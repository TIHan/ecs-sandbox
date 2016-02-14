namespace ECS

type Entities = EntityManager

type SystemUpdate = SystemUpdate of (unit -> unit)

type ISystem =

    abstract Init : Entities * Events -> SystemUpdate

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System =

        interface ISystem

        new : string * (Entities -> Events -> SystemUpdate) -> System

    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> =

        interface ISystem

        new : (Entities -> 'T -> unit) -> EventQueue<'T>

    [<Sealed>]
    type EntityProcessor =

        interface ISystem

        new : unit -> EntityProcessor
