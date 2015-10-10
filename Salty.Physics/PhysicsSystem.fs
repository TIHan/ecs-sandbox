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

            world
            |> Component.added
            |> Observable.add (fun (ent, physics: Physics) ->
                let data = physics.Data

                physics.Internal.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)

                physics.Internal.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physics.Density.Value)
                physics.Internal.Fixture <- physics.Internal.Body.CreateFixture (physics.Internal.PolygonShape)
                physics.Internal.Fixture.UserData <- (ent, physics)
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

            world
            |> Component.removed
            |> Observable.add (fun (ent, physics: Physics) ->
                physics.Internal.Body.DestroyFixture (physics.Internal.Fixture)
            )

        member __.Update world =
            world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun _ physics position rotation ->
                physics.Internal.Body.Position <- position.Var.Value
                physics.Internal.Body.Rotation <- rotation.Var.Value
                physics.Internal.Body.LinearVelocity <- physics.Velocity.Value
            )

            physicsWorld.Step (single world.Dependency.Interval.Value.TotalSeconds)

            world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun ent physics position rotation ->
                if not physics.IsStatic.Value then
                    position.Var.Value <- physics.Internal.Body.Position
                    rotation.Var.Value <- physics.Internal.Body.Rotation
                    physics.Velocity.Value <- physics.Internal.Body.LinearVelocity

                    match world.ComponentQuery.TryGet<Centroid> ent with
                    | Some centroid ->
                        centroid.Var.Value <- physics.Internal.Body.WorldCenter
                    | _ -> ()
            )