namespace ECS.Core

open System

type internal EntityEvent =
    | Created of Entity
    | Spawned of Entity
    | Destroyed of Entity

    interface IEvent

type internal AnyComponentAdded = AnyComponentAdded of (Entity * IComponent * Type) with

    interface IEvent

type internal AnyComponentRemoved = AnyComponentRemoved of (Entity * IComponent * Type) with

    interface IEvent

type internal ComponentAdded<'T> = ComponentAdded of (Entity * 'T) with

    interface IEvent

type internal ComponentRemoved<'T> = ComponentRemoved of (Entity * 'T) with

    interface IEvent

[<Sealed>]
type internal EntityManager =
    
    interface IComponentQuery

    interface IComponentService

    interface IEntityService

    member Process : unit -> unit

    new : IEventAggregator * entityAmount: int -> EntityManager