namespace ECS.Core

open System

type internal EntityEvent =
    | Created of Entity
    | Spawned of Entity
    | Destroyed of Entity

    interface IEvent

type ComponentEvent =
    | AnyAdded of Entity * obj * Type
    | AnyRemoved of Entity * obj * Type

    interface IEvent

type internal ComponentEvent<'T> =
    | Added of Entity * 'T
    | Removed of Entity * 'T

    interface IEvent

type IComponentQuery =

    abstract Has<'T when 'T :> IComponent> : Entity -> bool

    abstract TryGet : Entity * Type -> IComponent option

    abstract TryGet<'T when 'T :> IComponent> : Entity -> 'T option

    abstract TryFind<'T when 'T :> IComponent> : (Entity * 'T -> bool) -> (Entity * 'T) option

    abstract Get<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract Get<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []

    abstract ForEach<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 * 'T4 -> unit) -> unit

    abstract ParallelForEach<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit

type IComponentService =

    abstract Add<'T when 'T :> IComponent> : Entity -> 'T -> unit

    abstract Remove<'T when 'T :> IComponent> : Entity -> unit

type IEntityService =

    abstract Spawn : Entity -> unit

    abstract Destroy : Entity -> unit

[<Sealed>]
type internal EntityManager =
    
    interface IComponentQuery

    interface IComponentService

    interface IEntityService

    member Process : unit -> unit

    new : IEventAggregator * entityAmount: int -> EntityManager