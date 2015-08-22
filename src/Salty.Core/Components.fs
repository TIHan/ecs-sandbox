namespace Salty.Core.Components

open System
open System.IO
open System.Numerics
open System.Collections.Generic
open System.Xml
open System.Xml.Serialization

open ECS.Core

type ISerializableComponent =
    inherit IComponent
    inherit IXmlSerializable

type Xml =
    {
        Writer: XmlWriter
        Reader: XmlReader
    }

    static member Create (outputStream: Stream, inputStream: Stream) =
        {
            Writer = XmlWriter.Create (outputStream)
            Reader = XmlReader.Create (inputStream)
        }

type SerializationSystem () =
    let outputStream = File.Open ("entity.xml", FileMode.OpenOrCreate)
    let inputStream = new MemoryStream ()

    interface ISystem with

        member this.Init world =
            world
            |> Entity.anyComponentAdded
            |> Observable.add (fun (entity, o, t) ->
                let id = entity.Id
                ()
            )

        member this.Update world =
            outputStream.Position <- 0L
            outputStream.SetLength (0L)
            inputStream.Position <- 0L
            inputStream.SetLength (0L)


type Centroid () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Position () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent
