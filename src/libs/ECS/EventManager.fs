namespace ECS

open System
open System.Collections.Concurrent

type IECSEvent = interface end

[<Sealed>]
type EventManager () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    member __.Listen<'T when 'T :> IECSEvent> f =
        let event = lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj))
        (event :?> Event<'T>).Publish.Add f

    member __.Publish (event: 'T when 'T :> IECSEvent) =
        let mutable value = Unchecked.defaultof<obj>
        if lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger event

    member __.GetEvent<'T when 'T :> IECSEvent> () =
       lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj)) :?> Event<'T>

type Events = EventManager

[<RequireQualifiedAccess>]
[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module EventManager =

    module Unsafe =

        let event<'T when 'T :> IECSEvent> (eventManager: EventManager) =
            eventManager.GetEvent<'T> ()
