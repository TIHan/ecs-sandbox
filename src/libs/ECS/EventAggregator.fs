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

    member __.Publish (eventValue: 'T when 'T :> IEvent) =
        let mutable value = Unchecked.defaultof<obj>
        if lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger eventValue

    member inline __.GetEvent<'T when 'T :> IEvent> () =
       lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj)) :?> Event<'T>

[<RequireQualifiedAccess>]
[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module EventAggregator =

    module Unsafe =

        let getEvent<'T when 'T :> IEvent> (eventAggregator: EventAggregator) =
            eventAggregator.GetEvent<'T> ()
