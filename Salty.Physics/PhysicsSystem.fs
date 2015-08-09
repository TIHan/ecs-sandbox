namespace Salty.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Physics.Components

open System.Numerics

type PhysicsSystem () =

    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))

    interface ISystem with
        
        member __.Init world =
            World.componentAdded<Physics> world
            |> Observable.add (function
                | (entity, physicsPolygon) ->
                    let data = 
                        physicsPolygon.Data.Value

                    physicsPolygon.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)
                    physicsPolygon.Body.BodyType <- if physicsPolygon.IsStatic.Value then FarseerPhysics.Dynamics.BodyType.Static else FarseerPhysics.Dynamics.BodyType.Dynamic
                    physicsPolygon.Body.Restitution <- physicsPolygon.Restitution.Value
                    physicsPolygon.Body.Friction <- physicsPolygon.Friction.Value
                    physicsPolygon.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physicsPolygon.Density.Value)
                    physicsPolygon.Fixture <- physicsPolygon.Body.CreateFixture (physicsPolygon.PolygonShape)
                    physicsPolygon.Fixture.UserData <- entity.Id
                    physicsPolygon.Body.Mass <- physicsPolygon.Mass.Value

                    physicsPolygon.Fixture.OnCollision <-
                        new FarseerPhysics.Dynamics.OnCollisionEventHandler (
                            fun fixture1 fixture2 _ -> 
                                true
                        )
            )

        member __.Update world =
            world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                physicsPolygon.Body.Position <- position.Var |> Var.value
                physicsPolygon.Body.Rotation <- rotation.Var |> Var.value
                physicsPolygon.Body.Awake <- true
            )

            physicsWorld.Step (single world.Time.Interval.Value.TotalSeconds)

            world.ComponentQuery.ForEach<Physics, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                position.Var.Value <- physicsPolygon.Body.Position
                rotation.Var.Value <- physicsPolygon.Body.Rotation

                match world.ComponentQuery.TryGet<Centroid> entity with
                | Some centroid ->
                    centroid.Var.Value <- physicsPolygon.Body.WorldCenter
                | _ -> ()
            )
        