namespace ECS

type IEvent = interface end

[<Sealed>]
type EventAggregator =

    internal new : unit -> EventAggregator

    member Listen : (#IEvent -> unit) -> unit

    member Publish : #IEvent -> unit

[<RequireQualifiedAccess>]
[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module EventAggregator =

    module Unsafe =

        val getEvent<'T when 'T :> IEvent> : EventAggregator -> Event<'T>