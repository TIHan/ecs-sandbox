namespace Salty.Core

open System

[<ReferenceEquality>]
type Var<'T when 'T : equality> =
    private {
        mutable value: 'T
        observers: ResizeArray<IObserver<'T>>
        subscriptions: ResizeArray<IDisposable>
        mutable isDisposed: bool
    }

    member this.Value
        with get () = this.value
        and set value = 
            if not <| value.Equals this.value then
                this.value <- value
                for i = 0 to this.observers.Count - 1 do
                    let observer = this.observers.[i]
                    observer.OnNext value

    member this.Listen (source: IObservable<'T>) =
        let s =
            source
            |> Observable.subscribe (fun x -> this.Value <- x)
        this.subscriptions.Add s              

    interface IObservable<'T> with

        member this.Subscribe observer =
            this.observers.Add observer
            observer.OnNext this.Value
            {
                new IDisposable with

                    member __.Dispose () =
                        this.observers.Remove observer |> ignore
            }

    interface IDisposable with

        member this.Dispose () =
            if not this.isDisposed then
                this.subscriptions
                |> Seq.iter (fun x -> x.Dispose ())
                this.isDisposed <- true

    static member inline (!!) (var: Var<'T>) = var.Value
    static member inline (<--) (var: Var<'T>, value: 'T) = var.Value <- value

module Var =

    let create initialValue =
        {
            value = initialValue
            observers = ResizeArray ()
            subscriptions = ResizeArray ()
            isDisposed = false
        }

[<Sealed>]
type Val<'T when 'T : equality> (initialValue, source: IObservable<'T>) =
    let observers = ResizeArray<IObserver<'T>> ()
    let mutable value = initialValue
    let mainObserver = 
        {
            new IObserver<'T> with

                member __.OnNext x =
                    if not <| value.Equals x then
                        value <- x
                        for i = 0 to observers.Count - 1 do
                            let observer = observers.[i]
                            observer.OnNext x

                member __.OnError x = 
                    for i = 0 to observers.Count - 1 do
                        let observer = observers.[i]
                        observer.OnError x

                member __.OnCompleted () =
                    for i = 0 to observers.Count - 1 do
                        let observer = observers.[i]
                        observer.OnCompleted ()
        }

    let mutable subscription = source.Subscribe mainObserver

    member this.Value = value

    interface IObservable<'T> with 
             
        member __.Subscribe observer = 
            observers.Add observer
            observer.OnNext value
            {
                new IDisposable with

                    member __.Dispose () =
                        observers.Remove observer |> ignore
            }

    interface IDisposable with

        member __.Dispose () = 
            subscription.Dispose ()
            mainObserver.OnCompleted ()

module Val =

    let create initialValue source =
        new Val<'T> (initialValue, source)

    let createConstant initialValue =
        new Val<'T> (initialValue, { new IObservable<'T> with member __.Subscribe _ = { new IDisposable with member __.Dispose () = () } })

    let ofVar (var: Var<'T>) =
        new Val<'T> (var.Value, var)