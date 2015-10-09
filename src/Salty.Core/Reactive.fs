namespace Salty.Core

open System

[<Sealed>]
type Val<'T when 'T : equality> (initialValue, source: IObservable<'T>) =
    let observers = ResizeArray<IObserver<'T>> ()
    let mutable value = initialValue

    do
        source |> Observable.add (fun x -> 
            value <- x
        )

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

[<ReferenceEquality>]
type Var<'T when 'T : equality> =
    private {
        mutable value: 'T
        observers: ResizeArray<IObserver<'T>>
    }

    member this.Value
        with get () = this.value
        and set value = 
            if not <| value.Equals this.value then
                this.value <- value
                for i = 0 to this.observers.Count - 1 do
                    let observer = this.observers.[i]
                    observer.OnNext value    
                          
    member this.ToObservable () =
        let weakThis = WeakReference<Var<'T>> (this)
        {
            new IObservable<'T> with

                member __.Subscribe observer =
                    match weakThis.TryGetTarget () with
                    | false, _ -> { new IDisposable with member __.Dispose () = () }
                    | _, this ->
                        this.observers.Add observer
                        observer.OnNext this.Value
                        {
                            new IDisposable with

                                member __.Dispose () =
                                    this.observers.Remove observer |> ignore
                        }
        }

    static member inline (!!) (var: Var<'T>) = var.Value
    static member inline (<--) (var: Var<'T>, value: 'T) = var.Value <- value

module Val =

    let create initialValue source =
        new Val<'T> (initialValue, source)

    let createConstant initialValue =
        new Val<'T> (initialValue, { new IObservable<'T> with member __.Subscribe _ = { new IDisposable with member __.Dispose () = () } })

    let ofVar (var: Var<'T>) =
        new Val<'T> (var.Value, var)

module Var =

    let create initialValue =
        {
            value = initialValue
            observers = ResizeArray ()
        }
