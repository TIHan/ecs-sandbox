namespace Salty.Core.Components

open System
open System.Numerics
open System.Xml.Serialization
open System.Globalization

open ECS.Core

open Salty.Core

type Centroid () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Position () =

    member val Var = Var.create Vector2.Zero with get

    interface ISerializableComponent

    interface IComponent

    interface IXmlSerializable with

        member __.GetSchema () = null

        member this.WriteXml writer =
            let position = this.Var.Value
            writer.WriteAttributeString ("X", position.X.ToString ("F"))
            writer.WriteAttributeString ("Y", position.Y.ToString ("F"))

        member this.ReadXml reader =
            let mutable position = Vector2 ()

            position.X <- Single.Parse (reader.GetAttribute ("X"), NumberStyles.Number, CultureInfo.InvariantCulture)
            position.Y <- Single.Parse (reader.GetAttribute ("Y"), NumberStyles.Number, CultureInfo.InvariantCulture)

            this.Var.Value <- position

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent
