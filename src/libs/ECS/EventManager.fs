namespace ECS

open System
open System.Collections.Concurrent

type IECSEvent = interface end

[<Sealed>]
type EventManager () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    member __.Publish (event: 'T when 'T :> IECSEvent) =
        let mutable value = Unchecked.defaultof<obj>
        if lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger event

    member __.GetEvent<'T when 'T :> IECSEvent> () =
       lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj)) :?> Event<'T>

type Events = EventManager
