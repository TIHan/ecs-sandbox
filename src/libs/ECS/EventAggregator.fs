namespace ECS

open System
open System.Collections.Concurrent

type IEvent = interface end

[<Sealed>]
type EventAggregator () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    member __.Listen<'T when 'T :> IEvent> f =
        let event = lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj))
        (event :?> Event<'T>).Publish.Add f

    member __.Publish (eventValue: #IEvent) =
        let mutable value = Unchecked.defaultof<obj>
        if lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger eventValue
