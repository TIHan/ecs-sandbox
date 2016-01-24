namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

[<Sealed>]
type System =

    static member Append : ISystem -> ISystem -> ISystem

    static member Empty : ISystem

[<Sealed>]
type EventListener<'Event when 'Event :> IEvent> =

    new : (EntityManager -> 'Event -> unit) -> EventListener<'Event>

    interface ISystem

[<Sealed>]
type EventQueue<'Event when 'Event :> IEvent> =

    new : (EntityManager -> 'Event -> unit) -> EventQueue<'Event>

    interface ISystem

[<Sealed>]
type EntityProcessor =

    new : unit -> EntityProcessor

    interface ISystem
