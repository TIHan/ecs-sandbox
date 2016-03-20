open System
open System.IO
open System.Collections.Generic
open System.Reflection
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open FSharp.ECS
open FSharp.ECS.World
open LitePickler.Pickle
open LitePickler.Unpickle
open LitePickler.Core

// FIXME: work in progress
module Serialization =

    type Serialize = delegate of obj * LiteWriteStream -> unit

    type Deserialize = delegate of byref<obj> * LiteReadStream -> unit

    type SerializeProperty = delegate of obj * LiteWriteStream -> unit

    type DeserializeProperty = delegate of obj * LiteReadStream -> unit

    let properties (typ: Type) = 
        let props =
            typ.GetProperties ()
            |> List.ofSeq
        props

    let defaultSerializableTypes = Dictionary<Type, Serialize * Deserialize> ()

    let rec isTypeAutoSerializable (typ: Type) =
        defaultSerializableTypes.ContainsKey typ ||
        properties typ
        |> List.forall (fun x ->
            x.CanWrite && x.CanRead &&
            defaultSerializableTypes.ContainsKey (x.PropertyType)
        )

    let autoAdd<'T when 'T : unmanaged> =
        defaultSerializableTypes.Add (typeof<'T>,
            (
                Serialize (fun o stream -> LiteWriteStream.write<'T> (o :?> 'T) stream), 
                Deserialize (fun r stream -> r <- (LiteReadStream.read<'T> stream) :> obj)
            )
        )

    let add<'T> serialize deserialize =
        defaultSerializableTypes.Add (typeof<'T>,
            (
                Serialize (fun o stream -> serialize (o :?> 'T) stream), 
                Deserialize (fun r stream -> r <- (deserialize stream) :> obj)
            )
        )

    do
        autoAdd<byte>
        autoAdd<sbyte>
        autoAdd<uint16>
        autoAdd<int16>
        autoAdd<uint32>
        autoAdd<int32>
        autoAdd<uint64>
        autoAdd<int64>
        autoAdd<float32>
        autoAdd<float>
        autoAdd<Decimal>
        add<string> 
            (fun str stream -> 
                LiteWriteStream.write<int> str.Length stream
                LiteWriteStream.writeString str.Length StringKind.UTF8 str stream
            ) 
            (fun stream -> 
                let length = LiteReadStream.read<int> stream
                LiteReadStream.readString length stream
            )

open Serialization

[<Sealed>]
type SerializationHelper =

    static member CreateGetterDelegate<'T, 'U> (prop: PropertyInfo) =
        let getter = prop.GetMethod
        let del = Delegate.CreateDelegate (typeof<Func<'T, 'U>>, getter) :?> Func<'T, 'U>
        Func<obj, obj> (fun x -> del.Invoke (x :?> 'T) :> obj)

    static member CreateSetterDelegate<'T, 'U> (prop: PropertyInfo) =
        let setter = prop.SetMethod
        let del = Delegate.CreateDelegate (typeof<Action<'T, 'U>>, setter) :?> Action<'T, 'U>
        Action<obj, obj> (fun x o -> del.Invoke (x :?> 'T, o :?> 'U))


    static member GenericAutoSerialize<'T> () =
        let typ = typeof<'T>

        let props =
            let props = ResizeArray ()
            if isTypeAutoSerializable typ then
                properties typ
                |> List.iter (fun prop ->
                    if isTypeAutoSerializable prop.PropertyType then
                        let serialize, deserialize = defaultSerializableTypes.[prop.PropertyType]

                        let createGetter = typeof<SerializationHelper>.GetMethod("CreateGetterDelegate")
                        let createGetter = createGetter.MakeGenericMethod ([|typ;prop.PropertyType|])
                        let createGetterDel = createGetter.Invoke (null, [|prop|]) :?> Func<obj, obj>

                        let createSetter = typeof<SerializationHelper>.GetMethod("CreateSetterDelegate")
                        let createSetter = createSetter.MakeGenericMethod ([|typ;prop.PropertyType|])
                        let createSetterDel = createSetter.Invoke (null, [|prop|]) :?> Action<obj, obj>

                        (
                            SerializeProperty (fun (inst: obj) stream -> serialize.Invoke (createGetterDel.Invoke (inst), stream)),
                            DeserializeProperty (fun (inst: obj) stream -> 
                                let mutable r = Unchecked.defaultof<obj>
                                deserialize.Invoke (&r, stream)
                                createSetterDel.Invoke (inst, r)
                            )
                        )
                        |> props.Add
                )
            props

        (
            Serialize (fun x stream -> props |> Seq.iter (fun (del, _) -> del.Invoke (x, stream))),
            Deserialize (fun o stream ->
                for i = 0 to props.Count - 1 do
                    let (_, del) = props.[i]
                    del.Invoke (o, stream)
            )
        )
        |> Some

[<CLIMutable>]
type TestComponent =
    {
        mutable Value: int
        mutable Message: string
    }                

    interface IEntityComponent

module TestSerialization =

    let stream = new MemoryStream ()
    let liteWriteStream = LiteWriteStream.ofStream stream
    let liteReadStream = LiteReadStream.ofStream stream

    let serialize, deserialize = 
        match SerializationHelper.GenericAutoSerialize<TestComponent> () with
        | Some (x) -> x
        | _ -> failwith "fuck"

    let s (arr: TestComponent []) (i: int) =
        serialize.Invoke (arr.[i], liteWriteStream)

    let ds (arr: TestComponent []) (i: int) =
        let mutable o = (arr.[i] :> obj)
        deserialize.Invoke (&o, liteReadStream)

[<EntryPoint>]
let main argv =
    let arr = Array.init 1000 (fun i -> { Value = i; Message = "butt" })
    let test () =
        let s = System.Diagnostics.Stopwatch.StartNew ()
        TestSerialization.stream.Position <- 0L
        for i = 0 to 1000 - 1 do
            TestSerialization.s arr i
        TestSerialization.stream.Position <- 0L
        for i = 0 to 1000 - 1 do
            TestSerialization.ds arr i
        s.Stop ()
        printfn "Time: %A ms" s.Elapsed.TotalMilliseconds

    for i = 0 to 1000 - 1 do
        test ()

    printfn "%A" arr.[0]
    0

