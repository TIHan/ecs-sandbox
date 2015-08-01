namespace ECS.Core

open System
open System.Collections.Concurrent

type IEvent = interface end

type IEventAggregator =

    abstract GetEvent<'T when 'T :> IEvent> : unit -> IObservable<'T>

    abstract Publish<'T when 'T :> IEvent> : 'T -> unit

[<Sealed>]
type EventAggregator () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    interface IEventAggregator with

        member __.GetEvent<'T when 'T :> IEvent> () : IObservable<'T> =
            let event = lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj))
            (event :?> Event<'T>).Publish :> IObservable<'T>

        member __.Publish<'T when 'T :> IEvent> eventValue =
            match lookup.TryGetValue typeof<'T> with
            | true, event -> (event :?> Event<'T>).Trigger eventValue
            | _ -> ()
