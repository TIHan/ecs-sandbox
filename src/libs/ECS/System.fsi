namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

module Systems =

    [<Sealed>]
    type EventQueue<'Event when 'Event :> IEvent> =

        interface ISystem

        static member Create : (EntityManager -> EventAggregator -> 'Event -> unit) -> EventQueue<'Event>

    [<Sealed>]
    type EntityProcessor =

        interface ISystem

        static member Create : unit -> EntityProcessor
