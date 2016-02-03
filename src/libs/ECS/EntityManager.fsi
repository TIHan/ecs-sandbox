namespace ECS.Core

open System

[<Struct>]
type Entity =

    val Id : int

    new : int -> Entity

type IComponent = interface end

type ComponentAdded<'T when 'T :> IComponent> = ComponentAdded of Entity with

    interface IEvent

type ComponentRemoved<'T when 'T :> IComponent> = ComponentRemoved of Entity with

    interface IEvent

type AnyComponentAdded = AnyComponentAdded of Entity * componentType: Type with

    interface IEvent

type AnyComponentRemoved = AnyComponentRemoved of Entity * componentType: Type with

    interface IEvent

type EntitySpawned = EntitySpawned of Entity with

    interface IEvent

type EntityDestroyed = EntityDestroyed of Entity with

    interface IEvent

[<Sealed>]
type EntityManager =

    // Component Query
    
    member Has<'T when 'T :> IComponent> : Entity -> bool

    member TryGet : Entity * Type -> IComponent option

    member TryGet : Entity * byref<#IComponent> -> unit

    member TryGet<'T when 'T :> IComponent> : Entity -> 'T option

    member TryFind<'T when 'T :> IComponent> : (Entity -> 'T -> bool) -> (Entity * 'T) option

    member GetAll<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    member GetAll<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []

    member ForEach<'T when 'T :> IComponent> : (Entity -> 'T -> unit) -> unit

    member ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    member ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

    member ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit) -> unit

    member ParallelForEach<'T when 'T :> IComponent> : (Entity -> 'T -> unit) -> unit

    member ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    member ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

    // Components

    member internal AddComponent<'T when 'T :> IComponent> : Entity -> 'T -> unit

    member internal RemoveComponent<'T when 'T :> IComponent> : Entity -> unit

    // Entites

    member internal Spawn : (Entity -> unit) -> unit

    member Destroy : Entity -> unit

    member internal Process : unit -> unit

    internal new : EventAggregator * entityAmount: int -> EntityManager