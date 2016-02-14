namespace ECS

type SystemUpdate = SystemUpdate of (unit -> unit)

[<AbstractClass>]
type HandleEvent = 

    abstract internal Handle : Entities * Events -> unit

type HandleEvent<'T when 'T :> IECSEvent> =
    inherit HandleEvent

    new : (Entities -> 'T -> unit) -> HandleEvent<'T>

type IECSSystem =

    abstract HandleEvents : HandleEvent list

    abstract Init : Entities * Events -> SystemUpdate

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System =

        interface IECSSystem

        new : string * HandleEvent list * (Entities -> Events -> SystemUpdate) -> System

    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> =

        interface IECSSystem

        new : (Entities -> 'T -> unit) -> EventQueue<'T>

    [<Sealed>]
    type EntityProcessor =

        interface IECSSystem

        new : unit -> EntityProcessor
