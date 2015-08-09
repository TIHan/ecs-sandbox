namespace Salty.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components

open System
open System.IO
open System.Numerics
open System.Xml
open System.Xml.Serialization
open System.Globalization

module Components =

    type Physics () =

        member val Data : Vector2 [] Var = Var.create [||]

        member val IsStatic = Var.create false

        member val Density = Var.create 0.f

        member val Restitution = Var.create 0.f

        member val Friction = Var.create 0.f

        member val Mass = Var.create 0.f

        member val Body : FarseerPhysics.Dynamics.Body = null with get, set

        member val PolygonShape : FarseerPhysics.Collision.Shapes.PolygonShape = null with get, set

        member val Fixture : FarseerPhysics.Dynamics.Fixture = null with get, set

        interface IComponent<Physics>

        interface IXmlSerializable with

            member this.GetSchema () = null

            member this.WriteXml writer =
                writer.WriteAttributeString ("IsStatic", this.IsStatic.Value.ToString ())
                writer.WriteAttributeString ("Density", this.IsStatic.Value.ToString ())
                writer.WriteAttributeString ("Restitution", this.Restitution.Value.ToString ())
                writer.WriteAttributeString ("Friction", this.Friction.Value.ToString ())
                writer.WriteAttributeString ("Mass", this.Mass.Value.ToString ())

            member this.ReadXml reader =
                this.IsStatic.Value <- bool.Parse (reader.GetAttribute ("IsStatic"))
                this.Density.Value <- Single.Parse (reader.GetAttribute ("Density"), NumberStyles.Number, CultureInfo.InvariantCulture)
                this.Restitution.Value <- Single.Parse (reader.GetAttribute ("Restitution"), NumberStyles.Number, CultureInfo.InvariantCulture)
                this.Friction.Value <- Single.Parse (reader.GetAttribute ("Friction"), NumberStyles.Number, CultureInfo.InvariantCulture)
                this.Mass.Value <- Single.Parse (reader.GetAttribute ("Mass"), NumberStyles.Number, CultureInfo.InvariantCulture)

open Components

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