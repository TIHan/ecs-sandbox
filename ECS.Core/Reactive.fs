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
    let mutable subscription = source.Subscribe subject.OnNext

    member this.Value = subject.Value

    member this.Assign (newSource: IObservable<'T>) =
        subscription.Dispose ()
        subscription <- newSource.Subscribe subject.OnNext

    member this.Dispose () = (this :> IDisposable).Dispose ()

    interface IObservable<'T> with 
             
        member __.Subscribe observer = 
            subject.Subscribe observer

    interface IDisposable with

        member __.Dispose () = 
            subscription.Dispose ()
            subject.Dispose ()

module ReactiveVal =

    let create initialValue =
        new ReactiveVal<'T> (initialValue, Observable.Never ())

    let createWithObservable initialValue source =
        new ReactiveVal<'T> (initialValue, source)

    let inline value (reactVal: ReactiveVal<'T>) = reactVal.Value

[<Sealed>]
type ReactiveTimeVal<'T> (initialValue, time: IObservable<TimeSpan>, source: IObservable<'T>) =
    let value = ReactiveVal.createWithObservable initialValue source
    let timeVal = ReactiveVal.createWithObservable TimeSpan.Zero time
    let observable =
        timeVal.DistinctUntilChanged().Select(fun _ -> value.Value).DistinctUntilChanged()

    member this.Value = value.Value

    member this.Assign (newSource: IObservable<'T>) =
        value.Assign newSource

    member this.AssignTime (time: IObservable<TimeSpan>) =
        timeVal.Assign time

    member this.Dispose () = (this :> IDisposable).Dispose ()

    interface IObservable<'T> with 
             
        member __.Subscribe observer = 
            observable.Subscribe observer

    interface IDisposable with

        member __.Dispose () = 
            value.Dispose ()

[<Sealed>]
type ReactivePrevVal<'T> (initialValue, time: IObservable<TimeSpan>, source: IObservable<'T>) =
    let value = new ReactiveTimeVal<'T> (initialValue, time, source)
    let previousVal = ReactiveVal.createWithObservable initialValue value

    member this.Value = value.Value

    member this.PreviousValue = previousVal.Value

    member this.Assign (newSource: IObservable<'T>) =
        value.Assign newSource

    member this.AssignTime (time: IObservable<TimeSpan>) =
        value.AssignTime time

    member this.Dispose () = (this :> IDisposable).Dispose ()

    interface IObservable<'T * 'T> with 
             
        member __.Subscribe observer = 
            value.Zip(previousVal, fun x y -> (x, y)).Subscribe (observer)

    interface IDisposable with

        member __.Dispose () = 
            value.Dispose ()
            previousVal.Dispose ()

module ReactivePrevVal =

    let create initialValue =
        new ReactivePrevVal<'T> (initialValue, Observable.Never (), Observable.Never ())

    let createWithObservable initialValue source =
        new ReactivePrevVal<'T> (initialValue, Observable.Never (), source)

    let createWithTime initialValue time =
        new ReactivePrevVal<'T> (initialValue, time, Observable.Never ())

    let createWithTimeAndObservable initialValue time source =
        new ReactivePrevVal<'T> (initialValue, time, source)