namespace Salty.Core.Physics

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

    let onCollided : World -> IObservable<(Entity * Physics) * (Entity * Physics)> =
        fun world ->
            world.EventAggregator.GetEvent<Collided> ()
            |> Observable.map (function
                | Collided x -> x
            )
    
    let applyImpulse (force: Vector2) (physics: Physics) (world: World) : unit =
        physics.Internal.Body.ApplyLinearImpulse (force)
        physics.Velocity <- physics.Internal.Body.LinearVelocity

type PhysicsSystem () =

    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))

    interface ISystem with
        
        member __.Init world =

            Component.onAdded world |> Observable.add (fun (ent, physics: Physics) ->
                let data = physics.Data

                physics.Internal.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)

                physics.Internal.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physics.Density)
                physics.Internal.Fixture <- physics.Internal.Body.CreateFixture (physics.Internal.PolygonShape)
                physics.Internal.Fixture.UserData <- (ent, physics)
                physics.Internal.Body.BodyType <- if physics.IsStatic then FarseerPhysics.Dynamics.BodyType.Static else FarseerPhysics.Dynamics.BodyType.Dynamic
                physics.Internal.Body.Restitution <- physics.Restitution
                physics.Internal.Body.Friction <- physics.Friction
                physics.Internal.Body.Mass <- physics.Mass

                physics.Internal.Fixture.OnCollision <-
                    new FarseerPhysics.Dynamics.OnCollisionEventHandler (
                        fun fixture1 fixture2 contact ->
                            let phys1 = fixture1.UserData :?> (Entity * Physics)
                            let phys2 = fixture2.UserData :?> (Entity * Physics)
                            world.EventAggregator.Publish (Collided (phys1, phys2))
                            true
                    )
            )
//
//            upon Component.removed <| fun (ent, physics: Physics) ->
//                physics.Internal.Body.DestroyFixture (physics.Internal.Fixture)
//                DoNothing
//
//            (
//                uponSpawn2 <| fun ent (physics: Physics) (position: Position) ->
//                    [
//                        position.Var |> pushTo physics.Position
//                    ]
//            ) world
//
//            (
//                uponSpawn2 <| fun ent (physics: Physics) (rotation: Rotation) ->
//                    [
//                        rotation.Var |> pushTo physics.Rotation
//                    ]
//            ) world

        member __.Update world =
            match world.ComponentQuery.TryFind<WorldTime> (fun _ _ -> true) with
            | None -> ()
            | Some (_, time) ->
                world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun entity physics position rotation ->
                    if not physics.IsStatic then
                        physics.Internal.Body.Position <- position.Value
                        physics.Internal.Body.Rotation <- rotation.Value
                        physics.Internal.Body.LinearVelocity <- physics.Velocity
                )

                physicsWorld.Step (single time.Interval.TotalSeconds)

                world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun entity physics position rotation ->
                    if not physics.IsStatic then
                        position.Value <- physics.Internal.Body.Position
                        rotation.Value <- physics.Internal.Body.Rotation
                        physics.Velocity <- physics.Internal.Body.LinearVelocity

                        match world.ComponentQuery.TryGet<Centroid> entity with
                        | Some centroid ->
                            centroid.Value <- physics.Internal.Body.WorldCenter
                        | _ -> ()
                )