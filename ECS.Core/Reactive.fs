namespace ECS.Core

open System
open System.Reactive.Linq
open System.Reactive.Subjects

[<Sealed>]
type ReactiveVar<'T> (initialValue) =
    let subject = new BehaviorSubject<'T> (initialValue)

    member this.Value
        with get () = subject.Value
        and set value = subject.OnNext value

    interface IObservable<'T> with

        member __.Subscribe observer =
            subject.Subscribe observer

    interface IDisposable with

        member __.Dispose () =
            subject.Dispose ()

module ReactiveVar =

    let create initialValue =
        new ReactiveVar<'T> (initialValue)

    let inline value (reactVar: ReactiveVar<'T>) = reactVar.Value

    let inline setValue value (reactVar: ReactiveVar<'T>) =
        reactVar.Value <- value

[<Sealed>]
type ReactiveVal<'T> (initialValue, source: IObservable<'T>) =
    let subject = new BehaviorSubject<'T> (initialValue)
    let subscription = source.Subscribe subject.OnNext

    member this.Value = subject.Value

    member this.Dispose () = (this :> IDisposable).Dispose ()

    interface IObservable<'T> with 
             
        member __.Subscribe observer = 
            subject.Subscribe observer

    interface IDisposable with

        member __.Dispose () = 
            subscription.Dispose ()
            subject.Dispose ()

module ReactiveVal =

    let create initialValue source =
        new ReactiveVal<'T> (initialValue, source)

    let inline value (reactVal: ReactiveVal<'T>) = reactVal.Value
