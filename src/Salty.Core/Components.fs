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

type SerializationSystem () =
    let outputStream = File.Open ("game.xml", FileMode.OpenOrCreate)
    let inputStream = new MemoryStream ()

    let entities : (ResizeArray<ISerializableComponent> []) = Array.init 65536 (fun _ -> ResizeArray ())

    interface ISystem with

        member this.Init world =
            world
            |> Entity.anyComponentAdded
            |> Observable.add (fun (entity, o, t) ->
                if typeof<ISerializableComponent>.IsAssignableFrom t then
                    let id = entity.Id
                    entities.[id].Add (o :?> ISerializableComponent)
            )

        member this.Update world =
      
            outputStream.Position <- 0L
            outputStream.SetLength (0L)
            inputStream.Position <- 0L
            inputStream.SetLength (0L)

            let settings = XmlWriterSettings();
            settings.Indent <- true
            settings.IndentChars <- "\t"

            use writer = XmlWriter.Create (outputStream, settings)

            writer.WriteStartElement ("Game", "root")
            entities
            |> Array.iteri (fun i comps ->
                match comps with
                | comps when comps.Count.Equals 0 -> ()
                | comps ->
                    writer.WriteStartElement ("Entity")
                    writer.WriteAttributeString ("Id", i.ToString ())
                    comps.ForEach (fun comp ->
                        writer.WriteStartElement (comp.GetType().Name)
                        comp.WriteXml writer
                        writer.WriteEndElement ()
                    )
                    writer.WriteEndElement ()
            )
            writer.WriteEndElement ()


type Centroid () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Position () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent
