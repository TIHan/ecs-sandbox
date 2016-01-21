namespace ECS.Core

open System
open System.Collections.Concurrent

[<Sealed>]
type EventAggregator () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    member __.GetEvent<'T when 'T :> IEvent> () =
        let event = lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj))
        (event :?> Event<'T>).Publish :> IObservable<'T>

    member __.Publish (eventValue: #IEvent) =
        let mutable value = Unchecked.defaultof<obj>
        if lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger eventValue
