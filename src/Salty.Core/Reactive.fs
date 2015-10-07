namespace Salty.Core

open System

[<Sealed>]
type Var<'T> (initialValue) =
    let observers = ResizeArray<IObserver<'T>> ()
    let mutable currentValue = initialValue

    member this.Value
        with get () = currentValue
        and set value = 
            currentValue <- value
            for i = 0 to observers.Count - 1 do
                let observer = observers.[i]
                observer.OnNext value

    interface IObservable<'T> with

        member __.Subscribe observer =
            observers.Add observer
            observer.OnNext currentValue
            {
                new IDisposable with

                    member __.Dispose () =
                        observers.Remove observer |> ignore
            }

module Var =

    let create initialValue =
        new Var<'T> (initialValue)

[<Sealed>]
type Val<'T> (initialValue, source: IObservable<'T>) =
    let observers = ResizeArray<IObserver<'T>> ()
    let mutable value = initialValue
    let mainObserver = 
        {
            new IObserver<'T> with

                member __.OnNext x =
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

    member this.Listen (newSource: IObservable<'T>) =
        subscription.Dispose ()
        subscription <- newSource.Subscribe mainObserver

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