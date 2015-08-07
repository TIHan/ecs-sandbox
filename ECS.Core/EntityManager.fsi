namespace ECS.Core

open System

type EntityEvent =
    | Created of Entity
    | Destroyed of Entity

    interface IEvent

type ComponentEvent<'T> =
    | Added of Entity * 'T
    | Removed of Entity * 'T

    interface IEvent

type IComponentQuery =

    abstract Has<'T when 'T :> IComponent> : Entity -> bool

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

type IEntityFactory =

    abstract Create : id: int -> IComponent list -> unit

    abstract Destroy : Entity -> unit

    abstract AddComponent<'T when 'T :> IComponent> : Entity -> 'T -> unit

    abstract RemoveComponent<'T when 'T :> IComponent> : Entity -> unit

[<Sealed>]
type internal EntityManager =

    interface IEntityFactory
    
    interface IComponentQuery

    member Process : unit -> unit

    new : IEventAggregator * entityAmount: int -> EntityManager