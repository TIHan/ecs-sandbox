namespace Salty.Core.Components

open System
open System.IO
open System.Numerics
open System.Collections.Generic
open System.Xml.Serialization

open ECS.Core

type ISerializableComponent =
    inherit IComponent
    inherit IXmlSerializable

type SerializationSystem () =
    let ms = new MemoryStream ()
    let lookup = Dictionary<Type, XmlSerializer * (obj [])> ()

    interface ISystem with

        member this.Init world =
            world
            |> Entity.anyComponentAdded
            |> Observable.add (fun (entity, o, t) ->
                
                if typeof<ISerializableComponent>.IsAssignableFrom (t) then
                    match lookup.TryGetValue t with
                    | false, _ -> 
                        let s = XmlSerializer (t)
                        let arr = Array.init 65536 (fun _ -> null)

                        arr.[entity.Id] <- o
                        lookup.Add (t, (s, arr))
                    | _, (_, arr) ->
                        arr.[entity.Id] <- o

            )

        member this.Update world =
            ms.Position <- 0L
            ms.SetLength (0L)

            lookup.Values
            |> Seq.iter (fun (s, arr) ->
                arr
                |> Array.iter (fun x ->
                    if not <| obj.ReferenceEquals (x, null) then
                        s.Serialize (ms, x)
                )
            )


type Centroid () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Position () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent
