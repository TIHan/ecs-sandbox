namespace Salty.Core.Physics.Components

open ECS.Core

open Salty.Core
open Salty.Core.Components

open System
open System.IO
open System.Numerics
open System.Xml
open System.Xml.Serialization
open System.Globalization

type internal PhysicsInternal () =

    member val internal Body : FarseerPhysics.Dynamics.Body = null with get, set

    member val internal PolygonShape : FarseerPhysics.Collision.Shapes.PolygonShape = null with get, set

    member val internal Fixture : FarseerPhysics.Dynamics.Fixture = null with get, set

type Physics () =

    member val Data : Vector2 [] = [||] with get, set

    member val IsStatic = false with get, set

    member val Density = 0.f with get, set

    member val Restitution = 0.f with get, set

    member val Friction = 0.f with get, set

    member val Mass = 0.f with get, set

    member val Velocity = Vector2.Zero with get, set

    member val Position = Vector2.Zero with get, set

    member val Rotation = 0.f with get, set

    member val internal Internal = PhysicsInternal () with get, set

    interface ISerializableComponent

    interface IComponent

    interface IXmlSerializable with

        member this.GetSchema () = null

        member this.WriteXml writer =
            writer.WriteAttributeString ("IsStatic", this.IsStatic.ToString ())
            writer.WriteAttributeString ("Density", this.Density.ToString ())
            writer.WriteAttributeString ("Restitution", this.Restitution.ToString ())
            writer.WriteAttributeString ("Friction", this.Friction.ToString ())
            writer.WriteAttributeString ("Mass", this.Mass.ToString ())

        member this.ReadXml reader =
            this.IsStatic <- bool.Parse (reader.GetAttribute ("IsStatic"))
            this.Density <- Single.Parse (reader.GetAttribute ("Density"), NumberStyles.Number, CultureInfo.InvariantCulture)
            this.Restitution <- Single.Parse (reader.GetAttribute ("Restitution"), NumberStyles.Number, CultureInfo.InvariantCulture)
            this.Friction <- Single.Parse (reader.GetAttribute ("Friction"), NumberStyles.Number, CultureInfo.InvariantCulture)
            this.Mass <- Single.Parse (reader.GetAttribute ("Mass"), NumberStyles.Number, CultureInfo.InvariantCulture)

