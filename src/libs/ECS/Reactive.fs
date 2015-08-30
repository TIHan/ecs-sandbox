namespace ECS.Core

open System
open System.Reactive.Linq
open System.Reactive.Subjects

[<Sealed>]
type Var<'T when 'T : equality> (initialValue) =
    let subject = new BehaviorSubject<'T> (initialValue)

    member this.Value
        with get () = subject.Value
        and set value = 
            if not <| subject.Value.Equals value then
                subject.OnNext value

    interface IObservable<'T> with

        member __.Subscribe observer =
            subject.Subscribe observer

    interface IDisposable with

        member __.Dispose () =
            subject.Dispose ()

module Var =

    let create initialValue =
        new Var<'T> (initialValue)

    let inline value (reactVar: Var<'T>) = reactVar.Value

    let inline setValue value (reactVar: Var<'T>) =
        reactVar.Value <- value

[<Sealed>]
type Val<'T when 'T : equality> (initialValue, source: IObservable<'T>) =
    let subject = new BehaviorSubject<'T> (initialValue)
    let mutable subscription = source.Subscribe subject.OnNext

    member this.Value = subject.Value

    member this.Assign (newSource: IObservable<'T>) =
        subscription.Dispose ()
        subscription <- newSource.Subscribe subject.OnNext

    interface IObservable<'T> with 
             
        member __.Subscribe observer = 
            subject.DistinctUntilChanged().Subscribe observer

    interface IDisposable with

        member __.Dispose () = 
            subscription.Dispose ()
            subject.Dispose ()

module Val =

    let create initialValue =
        new Val<'T> (initialValue, Observable.Never ())

    let createWithObservable initialValue source =
        new Val<'T> (initialValue, source)

    let inline value (reactVal: Val<'T>) = reactVal.Value
