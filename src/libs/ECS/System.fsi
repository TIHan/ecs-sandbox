namespace ECS

[<AbstractClass>]
type HandleEvent =

    abstract internal Handle : Entities * Events -> unit

type HandleEvent<'T when 'T :> IECSEvent> =
    inherit HandleEvent

    new : (Entities -> Events -> 'T -> unit) -> HandleEvent<'T>

type IECSSystem =

    abstract HandleEvents : HandleEvent list

    abstract Update : Entities * Events -> unit

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System =

        interface IECSSystem

        new : string * HandleEvent list * (Entities -> Events -> unit) -> System

    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> =

        interface IECSSystem

        new : (Entities -> Events -> 'T -> unit) -> EventQueue<'T>

    [<Sealed>]
    type EntityProcessor =

        interface IECSSystem

        new : unit -> EntityProcessor
