namespace ECS.Core

open System
open System.Collections.Concurrent

[<Sealed>]
type EventAggregator () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    interface IEventAggregator with

        member __.GetEvent () =
            let event = lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj))
            (event :?> Event<'T>).Publish :> IObservable<#IEventData>

        member __.Publish eventValue =
            let mutable value = Unchecked.defaultof<obj>
            if lookup.TryGetValue (typeof<'T>, &value) then
                (value :?> Event<'T>).Trigger eventValue
