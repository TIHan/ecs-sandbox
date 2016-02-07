namespace ECS

type Entities = EntityManager
type Events = EventAggregator

type ISystem =

    abstract Init : Entities * Events -> unit

    abstract Update : Entities * Events -> unit

module Systems =

    [<Sealed>]
    type EventQueue<'Event when 'Event :> IEvent> =

        interface ISystem

        static member Create : (Entities -> Events -> 'Event -> unit) -> EventQueue<'Event>

    [<Sealed>]
    type EntityProcessor =

        interface ISystem

        static member Create : unit -> EntityProcessor
