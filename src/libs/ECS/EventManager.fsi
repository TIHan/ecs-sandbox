namespace ECS

type IECSEvent = interface end

[<Sealed>]
type EventManager =

    internal new : unit -> EventManager

    member Listen<'T when 'T :> IECSEvent> : ('T -> unit) -> unit

    member Publish : #IECSEvent -> unit

type Events = EventManager

[<RequireQualifiedAccess>]
[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module EventManager =

    module Unsafe =

        val event<'T when 'T :> IECSEvent> : EventManager -> Event<'T>