namespace ECS

type IECSEvent<'T> =

    abstract Data : 'T

[<Sealed>]
type EventManager =

    internal new : unit -> EventManager

    member Listen<'T, 'U when 'T :> IECSEvent<'U>> : ('U -> unit) -> unit

    member Publish : #IECSEvent<_> -> unit

type Events = EventManager

[<RequireQualifiedAccess>]
[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module EventManager =

    module Unsafe =

        val event<'T, 'U when 'T :> IECSEvent<'U>> : Events -> Event<'U>