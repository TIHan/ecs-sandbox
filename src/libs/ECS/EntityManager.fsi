namespace ECS.Core

open System

type internal EntitySpawned = EntitySpawned of Entity with

    interface IEventData

type internal EntityDestroyed = EntityDestroyed of Entity with

    interface IEventData

type internal AnyComponentAdded = AnyComponentAdded of (Entity * IComponent * Type) with

    interface IEventData

type internal AnyComponentRemoved = AnyComponentRemoved of (Entity * IComponent * Type) with

    interface IEventData

type internal ComponentAdded<'T> = ComponentAdded of (Entity * 'T) with

    interface IEventData

type internal ComponentRemoved<'T> = ComponentRemoved of (Entity * 'T) with

    interface IEventData

[<Sealed>]
type internal EntityManager =
    
    interface IComponentQuery

    interface IComponentService

    interface IEntityService

    member Process : unit -> unit

    new : IEventAggregator * entityAmount: int -> EntityManager