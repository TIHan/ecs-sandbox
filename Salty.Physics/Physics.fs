namespace Salty.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Physics.Components

open System
open System.IO
open System.Numerics
open System.Xml
open System.Xml.Serialization
open System.Globalization

type ApplyForceRequested = ApplyForceRequested of (Entity * Vector2) with

    interface IEvent

type Collided = Collided of ((Entity * Physics) * (Entity * Physics)) with

    interface IEvent

module Physics =

    let collided (world: IWorld) =
        world.EventAggregator.GetEvent<Collided> ()
        |> Observable.map (function
            | Collided x -> x
        )
    
    let applyForce force entity (world: IWorld) =
        world.EventAggregator.Publish (ApplyForceRequested (entity, force))

type PhysicsSystem () =

    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))

    interface ISystem with
        
        member __.Init world =
            World.componentAdded<Physics> world
            |> Observable.add (function
                | (entity, physicsPolygon) ->      

                    let data = 
                        physicsPolygon.Data.Value

                    physicsPolygon.Internal.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)

                    physicsPolygon.Position
                    |> Observable.add (fun position ->
                        physicsPolygon.Internal.Body.Position <- position
                    )

                    physicsPolygon.Rotation
                    |> Observable.add (fun rotation ->
                        physicsPolygon.Internal.Body.Rotation <- rotation
                    )

                    physicsPolygon.Internal.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physicsPolygon.Density.Value)
                    physicsPolygon.Internal.Fixture <- physicsPolygon.Internal.Body.CreateFixture (physicsPolygon.Internal.PolygonShape)
                    physicsPolygon.Internal.Fixture.UserData <- (entity, physicsPolygon)
                    physicsPolygon.Internal.Body.BodyType <- if physicsPolygon.IsStatic.Value then FarseerPhysics.Dynamics.BodyType.Static else FarseerPhysics.Dynamics.BodyType.Dynamic
                    physicsPolygon.Internal.Body.Restitution <- physicsPolygon.Restitution.Value
                    physicsPolygon.Internal.Body.Friction <- physicsPolygon.Friction.Value
                    physicsPolygon.Internal.Body.Mass <- physicsPolygon.Mass.Value

                    physicsPolygon.Internal.Fixture.OnCollision <-
                        new FarseerPhysics.Dynamics.OnCollisionEventHandler (
                            fun fixture1 fixture2 _ -> 
                                let phys1 = fixture1.UserData :?> (Entity * Physics)
                                let phys2 = fixture2.UserData :?> (Entity * Physics)
                                world.EventAggregator.Publish (Collided (phys1, phys2))
                                true
                        )
            )

            World.componentAdded<Physics> world
            |> Observable.add (fun (entity, physics) ->
                match world.ComponentQuery.TryGet<Position> entity with
                | Some position -> physics.Position.Assign (position.Var)
                | _ -> ()

                match world.ComponentQuery.TryGet<Rotation> entity with
                | Some rotation -> physics.Rotation.Assign (rotation.Var)
                | _ -> ()
            )

            World.componentAdded<Position> world
            |> Observable.add (fun (entity, position) ->
                match world.ComponentQuery.TryGet<Physics> entity with
                | Some physics -> physics.Position.Assign (position.Var)
                | _ -> ()
            )

            World.componentAdded<Rotation> world
            |> Observable.add (fun (entity, rotation) ->
                match world.ComponentQuery.TryGet<Physics> entity with
                | Some physics -> physics.Rotation.Assign (rotation.Var)
                | _ -> ()
            )

            world.EventAggregator.GetEvent<ApplyForceRequested> ()
            |> Observable.add (function
                | ApplyForceRequested (entity, force) ->
                    match world.ComponentQuery.TryGet<Physics> entity with
                    | Some physics ->
                        physics.Internal.Body.ApplyForce (force)
                    | _ -> ()
            )

            World.componentRemoved<Physics> world
            |> Observable.add (fun (_, physics) ->
                physics.Internal.Body.DestroyFixture (physics.Internal.Fixture)
                physics.Internal.Body.Dispose ()
                physics.Internal.Fixture.Dispose ()
            )

        member __.Update world =
            world.ComponentQuery.ForEach<Physics> (fun (entity, physics) ->
                if not physics.IsStatic.Value then
                    let mutable v = physics.Internal.Body.LinearVelocity
                    if v.X > 25.f then
                        v.X <- 25.f

                    if v.X < -25.f then
                        v.X <- -25.f

                    if v.Y > 25.f then
                        v.Y <- 25.f

                    if v.Y < -25.f then
                        v.Y <- -25.f

                    physics.Internal.Body.LinearVelocity <- v
            )

            physicsWorld.Step (single world.Time.Interval.Value.TotalSeconds)

            world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                if not physicsPolygon.IsStatic.Value then
                    position.Var.Value <- physicsPolygon.Internal.Body.Position
                    rotation.Var.Value <- physicsPolygon.Internal.Body.Rotation

                    match world.ComponentQuery.TryGet<Centroid> entity with
                    | Some centroid ->
                        centroid.Var.Value <- physicsPolygon.Internal.Body.WorldCenter
                    | _ -> ()
            )