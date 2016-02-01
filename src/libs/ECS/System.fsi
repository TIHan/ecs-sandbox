namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

[<Sealed>]
type EventSystem<'Event when 'Event :> IEvent> =

    new : (EntityManager -> 'Event -> unit) -> EventSystem<'Event>

    interface ISystem

[<Sealed>]
type EventQueueSystem<'Event when 'Event :> IEvent> =

    new : (EntityManager -> 'Event -> unit) -> EventQueueSystem<'Event>

    interface ISystem

[<Sealed>]
type EntityProcessorSystem =

    new : unit -> EntityProcessorSystem

    interface ISystem
