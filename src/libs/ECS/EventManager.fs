namespace ECS

open System
open System.Collections.Concurrent

type IECSEvent<'T> =

    abstract Data : 'T

[<Sealed>]
type EventManager () =
    let lookup = ConcurrentDictionary<Type, obj> ()

    member __.Listen<'T, 'U when 'T :> IECSEvent<'U>> f =
        let event = lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'U> () :> obj))
        (event :?> Event<'U>).Publish.Add f

    member __.Publish (event: 'T when 'T :> IECSEvent<'U>) =
        let mutable value = Unchecked.defaultof<obj>
        if lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'U>).Trigger event.Data

    member __.GetEvent<'T, 'U when 'T :> IECSEvent<'U>> () =
       lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'U> () :> obj)) :?> Event<'U>

type Events = EventManager

[<RequireQualifiedAccess>]
[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module EventManager =

    module Unsafe =

        let event<'T, 'U when 'T :> IECSEvent<'U>> (events: Events) =
            events.GetEvent<'T, 'U> ()
