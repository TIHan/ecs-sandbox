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

            world
            |> Entity.anyComponentRemoved
            |> Observable.add (fun (entity, o, t) ->
                if typeof<ISerializableComponent>.IsAssignableFrom t then
                    let id = entity.Id
                    entities.[id] <- ResizeArray ()
            )

        member this.Update world =
      
//            outputStream.Position <- 0L
//            outputStream.SetLength (0L)
//            inputStream.Position <- 0L
//            inputStream.SetLength (0L)

            let settings = XmlWriterSettings();
            settings.Indent <- true
            settings.IndentChars <- "\t"

            let outputStream = File.Open ("game.xml", FileMode.Create)
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
            writer.Dispose ()
            outputStream.Dispose ()

            let settings = XmlReaderSettings();
            settings.IgnoreWhitespace <- true
            settings.IgnoreComments <- true

            use inputStream = File.Open ("game.xml", FileMode.OpenOrCreate)
            use reader = XmlReader.Create (inputStream, settings)

            while reader.Read () do
                let mutable currentEntity = Entity ()
                if reader.IsStartElement () then
                    match reader.Name with
                    | "Game" -> ()
                    | "Entity" ->
                        currentEntity <- Entity (Int32.Parse (reader.GetAttribute("Id")))
                    | "Position" -> ()
                    | _ -> ()





type Centroid () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Position () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent
