namespace ECS.Core

open System
open System.Collections.Concurrent

type IEventAggregator =

    abstract GetEvent : unit -> IObservable<'T>

    abstract Publish : 'T -> unit

[<Sealed>]
type EventAggregator () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    interface IEventAggregator with

        member __.GetEvent<'T> () : IObservable<'T> =
            let event = lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj))
            (event :?> Event<'T>).Publish :> IObservable<'T>

        member __.Publish<'T> eventValue =
            match lookup.TryGetValue typeof<'T> with
            | true, event -> (event :?> Event<'T>).Trigger eventValue
            | _ -> ()
