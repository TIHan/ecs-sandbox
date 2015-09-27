﻿namespace Salty.Core.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Core.Physics.Components

open System
open System.IO
open System.Numerics
open System.Xml
open System.Xml.Serialization
open System.Globalization

type Collided = Collided of ((Entity * Physics) * (Entity * Physics)) with

    interface IEventData

module Physics =

    let collided : SaltyWorld<IObservable<(Entity * Physics) * (Entity * Physics)>> =
        fun world ->
            world.EventAggregator.GetEvent<Collided> ()
            |> Observable.map (function
                | Collided x -> x
            )
    
    let applyImpulse (force: Vector2) (physics: Physics) : SaltyWorld<unit> =
        fun world ->
            physics.Internal.Body.ApplyLinearImpulse (force)
            physics.Velocity.Value <- physics.Internal.Body.LinearVelocity

type PhysicsSystem () =

    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))

    interface ISystem<Salty> with
        
        member __.Init world =
            Component.added world
            |> Observable.add (function
                | (entity, physics: Physics) ->      

                    let data = 
                        physics.Data.Value

                    physics.Internal.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)

                    physics.Position
                    |> Observable.add (fun position ->
                        physics.Internal.Body.Position <- position
                    )

                    physics.Rotation
                    |> Observable.add (fun rotation ->
                        physics.Internal.Body.Rotation <- rotation
                    )

                    physics.Internal.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physics.Density.Value)
                    physics.Internal.Fixture <- physics.Internal.Body.CreateFixture (physics.Internal.PolygonShape)
                    physics.Internal.Fixture.UserData <- (entity, physics)
                    physics.Internal.Body.BodyType <- if physics.IsStatic.Value then FarseerPhysics.Dynamics.BodyType.Static else FarseerPhysics.Dynamics.BodyType.Dynamic
                    physics.Internal.Body.Restitution <- physics.Restitution.Value
                    physics.Internal.Body.Friction <- physics.Friction.Value
                    physics.Internal.Body.Mass <- physics.Mass.Value

                    physics.Internal.Fixture.OnCollision <-
                        new FarseerPhysics.Dynamics.OnCollisionEventHandler (
                            fun fixture1 fixture2 contact ->
                                let phys1 = fixture1.UserData :?> (Entity * Physics)
                                let phys2 = fixture2.UserData :?> (Entity * Physics)
                                world.EventAggregator.Publish (Collided (phys1, phys2))
                                true
                        )
            )

            Component.added world
            |> Observable.add (fun (entity, physics: Physics) ->
                match world.ComponentQuery.TryGet<Position> entity with
                | Some position -> physics.Position.Assign (position.Var)
                | _ -> ()

                match world.ComponentQuery.TryGet<Rotation> entity with
                | Some rotation -> physics.Rotation.Assign (rotation.Var)
                | _ -> ()
            )

            Component.added world
            |> Observable.add (fun (entity, position: Position) ->
                match world.ComponentQuery.TryGet<Physics> entity with
                | Some physics -> physics.Position.Assign (position.Var)
                | _ -> ()
            )

            Component.added world
            |> Observable.add (fun (entity, rotation: Rotation) ->
                match world.ComponentQuery.TryGet<Physics> entity with
                | Some physics -> physics.Rotation.Assign (rotation.Var)
                | _ -> ()
            )

            Component.removed world
            |> Observable.add (fun (_, physics: Physics) ->
                physics.Internal.Body.DestroyFixture (physics.Internal.Fixture)
            )

        member __.Update world =
            physicsWorld.Step (single world.Dependency.Interval.Value.TotalSeconds)

            world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun entity physicsPolygon position rotation ->
                if not physicsPolygon.IsStatic.Value then
                    position.Var.Value <- physicsPolygon.Internal.Body.Position
                    rotation.Var.Value <- physicsPolygon.Internal.Body.Rotation
                    physicsPolygon.Velocity.Value <- physicsPolygon.Internal.Body.LinearVelocity

                    match world.ComponentQuery.TryGet<Centroid> entity with
                    | Some centroid ->
                        centroid.Var.Value <- physicsPolygon.Internal.Body.WorldCenter
                    | _ -> ()
            )