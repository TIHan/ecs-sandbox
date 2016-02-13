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
    type EventQueue<'T, 'U when 'T :> IECSEvent<'U>> =

        interface ISystem

        new : (Entities -> Events -> 'U -> unit) -> EventQueue<'T, 'U>

    [<Sealed>]
    type EntityProcessor =

        interface ISystem

        new : unit -> EntityProcessor
