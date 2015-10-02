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

    member val Data : Vector2 [] Var = Var.create [||]

    member val IsStatic = Var.create false

    member val Density = Var.create 0.f

    member val Restitution = Var.create 0.f

    member val Friction = Var.create 0.f

    member val Mass = Var.create 0.f

    member val Velocity = Var.create Vector2.Zero

    member val Position = Val.create Vector2.Zero

    member val Rotation = Val.create 0.f

    member val internal Internal = PhysicsInternal () with get, set

    interface ISerializableComponent

    interface IComponent

    interface IXmlSerializable with

        member this.GetSchema () = null

        member this.WriteXml writer =
            writer.WriteAttributeString ("IsStatic", this.IsStatic.Value.ToString ())
            writer.WriteAttributeString ("Density", this.Density.Value.ToString ())
            writer.WriteAttributeString ("Restitution", this.Restitution.Value.ToString ())
            writer.WriteAttributeString ("Friction", this.Friction.Value.ToString ())
            writer.WriteAttributeString ("Mass", this.Mass.Value.ToString ())

        member this.ReadXml reader =
            __unsafe.setVarValueWithNotify this.IsStatic <| bool.Parse (reader.GetAttribute ("IsStatic"))
            __unsafe.setVarValueWithNotify this.Density <| Single.Parse (reader.GetAttribute ("Density"), NumberStyles.Number, CultureInfo.InvariantCulture)
            __unsafe.setVarValueWithNotify this.Restitution <| Single.Parse (reader.GetAttribute ("Restitution"), NumberStyles.Number, CultureInfo.InvariantCulture)
            __unsafe.setVarValueWithNotify this.Friction <| Single.Parse (reader.GetAttribute ("Friction"), NumberStyles.Number, CultureInfo.InvariantCulture)
            __unsafe.setVarValueWithNotify this.Mass <| Single.Parse (reader.GetAttribute ("Mass"), NumberStyles.Number, CultureInfo.InvariantCulture)

