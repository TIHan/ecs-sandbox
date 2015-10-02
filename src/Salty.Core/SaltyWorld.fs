namespace Salty.Core

open System
open System.Runtime.CompilerServices

open ECS.Core

[<Sealed>]
type Var<'T when 'T : equality> (initialValue) as this =

    [<DefaultValue>]
    val mutable value : 'T

    member val observers = ResizeArray<IObserver<'T>> ()

    do
        this.value <- initialValue

    member this.Value = this.value

    interface IObservable<'T> with

        member this.Subscribe observer =
            this.observers.Add observer
            observer.OnNext this.value
            {
                new IDisposable with

                    member __.Dispose () =
                        this.observers.Remove observer |> ignore
            }

module Var =

    let create initialValue =
        new Var<'T> (initialValue)

[<Sealed>]
type Val<'T when 'T : equality> (initialValue, source: IObservable<'T>) as this =

    [<DefaultValue>]
    val mutable value : 'T

    [<DefaultValue>]
    val mutable mainObserver : IObserver<'T>

    [<DefaultValue>]
    val mutable subscription : IDisposable

    member val observers : ResizeArray<IObserver<'T>> = ResizeArray<IObserver<'T>> ()

    do
        this.value <- initialValue
        this.mainObserver <-
            {
                new IObserver<'T> with

                    member __.OnNext x =
                        this.value <- x
                        for i = 0 to this.observers.Count - 1 do
                            let observer = this.observers.[i]
                            observer.OnNext x

                    member __.OnError x = 
                        for i = 0 to this.observers.Count - 1 do
                            let observer = this.observers.[i]
                            observer.OnError x

                    member __.OnCompleted () =
                        for i = 0 to this.observers.Count - 1 do
                            let observer = this.observers.[i]
                            observer.OnCompleted ()
            }
        this.subscription <- source.Subscribe this.mainObserver

    member __.Value = this.value

    interface IObservable<'T> with 
             
        member __.Subscribe observer = 
            this.observers.Add observer
            observer.OnNext this.value
            {
                new IDisposable with

                    member __.Dispose () =
                        this.observers.Remove observer |> ignore
            }

    interface IDisposable with

        member __.Dispose () = 
            this.subscription.Dispose ()
            this.mainObserver.OnCompleted ()

module Val =

    let create initialValue =
        new Val<'T> (initialValue, { new IObservable<'T> with member __.Subscribe _ = { new IDisposable with member __.Dispose () = () } })

    let ofVar (v: Var<'T>) =
        new Val<'T> (v.value, v)

type Salty =
    {
        CurrentTime: Val<TimeSpan>
        DeltaTime: Val<single>
        Interval: Val<TimeSpan>
    }

type SaltyWorld<'T> = World<Salty, 'T>

[<RequireQualifiedAccess>]
module __unsafe =

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let setVarValue (v: Var<'T>) (value: 'T) = 
        v.value <- value

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let setVarValueWithNotify (v: Var<'T>) (value: 'T) =
        v.value <- value
        for i = 0 to v.observers.Count - 1 do
            v.observers.[i].OnNext v.value

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let setValSource (v: Val<'T>) (source: IObservable<'T>) =
        v.subscription.Dispose ()
        v.subscription <- source.Subscribe v.mainObserver

[<AutoOpen>]
module DSL =

    let DoNothing : SaltyWorld<unit> = fun _ -> ()

    let inline worldReturn (a: 'a) : SaltyWorld<'a> = fun _ -> a

    let inline (>>=) (w: SaltyWorld<'a>) (f: 'a -> SaltyWorld<'b>) : SaltyWorld<'b> =
        fun world -> (f (w world)) world

    let inline (>>.) (w1: SaltyWorld<'a>) (w2: SaltyWorld<'b>) : SaltyWorld<'b> =
        fun world ->
            w1 world |> ignore
            w2 world

    let inline skip (x: SaltyWorld<_>) : SaltyWorld<unit> =
        fun world -> x world |> ignore

    let inline onEvent (w: SaltyWorld<IObservable<'a>>) (f: 'a -> SaltyWorld<unit>) : SaltyWorld<unit> =
        fun world ->
            (w world) 
            |> Observable.add (fun a ->
                (f a) world
            )

    let inline onUpdate (source: IObservable<'a>) (f: 'a -> SaltyWorld<unit>) : SaltyWorld<unit> =
        fun world -> source |> Observable.add (fun x -> (f x) world)

    let inline (==>) (source: IObservable<'a>) (v: Val<'a>) : SaltyWorld<unit> =
        fun world ->
            __unsafe.setValSource v source
            
    let inline update (v: Var<'a>) (f: 'a -> 'a) : SaltyWorld<unit> =
        fun world -> 
            __unsafe.setVarValueWithNotify v <| f v.Value

    let inline rule (f: Entity -> 'T -> SaltyWorld<unit>) : SaltyWorld<unit> =
        fun world ->
            Entity.spawned world |> Observable.add (fun ent ->

                let mutable c = Unchecked.defaultof<'T>
                world.ComponentQuery.TryGet (ent, &c)

                if not <| obj.ReferenceEquals (c, null) then
                    f ent c world
            )

    let inline rule2 (f: Entity -> 'T1 -> 'T2 -> SaltyWorld<unit>) : SaltyWorld<unit> =
        fun world ->
            Entity.spawned world |> Observable.add (fun ent ->

                let mutable c1 = Unchecked.defaultof<'T1>
                world.ComponentQuery.TryGet (ent, &c1)

                if not <| obj.ReferenceEquals (c1, null) then

                    let mutable c2 = Unchecked.defaultof<'T2>
                    world.ComponentQuery.TryGet (ent, &c2)

                    if not <| obj.ReferenceEquals (c2, null) then
                        f ent c1 c2 world
            )