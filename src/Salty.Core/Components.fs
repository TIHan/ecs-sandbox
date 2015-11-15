namespace Salty.Core.Components

open System
open System.Numerics
open System.Xml.Serialization
open System.Globalization

open ECS.Core

open Salty.Core

type Centroid =
    {
        mutable Value: Vector2
    }

    interface IComponent

type Position =
    {
        mutable Value: Vector2
    }

    interface ISerializableComponent

    interface IComponent

    interface IXmlSerializable with

        member __.GetSchema () = null

        member this.WriteXml writer =
            let position = this.Value
            writer.WriteAttributeString ("X", position.X.ToString ("F"))
            writer.WriteAttributeString ("Y", position.Y.ToString ("F"))

        member this.ReadXml reader =
            let mutable position = Vector2 ()

            position.X <- Single.Parse (reader.GetAttribute ("X"), NumberStyles.Number, CultureInfo.InvariantCulture)
            position.Y <- Single.Parse (reader.GetAttribute ("Y"), NumberStyles.Number, CultureInfo.InvariantCulture)

            this.Value <- position

type Rotation =
    {
        mutable Value: single
    }

    interface IComponent
