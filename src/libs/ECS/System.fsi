namespace ECS

type Entities = EntityManager
type Events = EventAggregator

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
    type EventQueue<'Event when 'Event :> IEvent> =

        interface ISystem

        new : (Entities -> Events -> 'Event -> unit) -> EventQueue<'Event>

    [<Sealed>]
    type EntityProcessor =

        interface ISystem

        new : unit -> EntityProcessor
