﻿namespace Salty.Core.Components

open System
open System.IO
open System.Numerics
open System.Collections.Generic
open System.Xml
open System.Xml.Serialization
open System.Globalization

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

    interface ISerializableComponent

    interface IComponent

    interface IXmlSerializable with

        member __.GetSchema () = null

        member this.WriteXml writer =
            let position = this.Var.Value
            writer.WriteAttributeString ("X", position.X.ToString ())
            writer.WriteAttributeString ("Y", position.Y.ToString ())

        member this.ReadXml reader =
            let mutable position = Vector2 ()

            position.X <- Single.Parse (reader.GetAttribute ("X"), NumberStyles.Number, CultureInfo.InvariantCulture)
            position.Y <- Single.Parse (reader.GetAttribute ("Y"), NumberStyles.Number, CultureInfo.InvariantCulture)

            this.Var.Value <- position

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent
