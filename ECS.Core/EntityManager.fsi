namespace ECS.Core

open System

type internal EntityEvent =
    | Created of Entity
    | Spawned of Entity
    | Destroyed of Entity

    interface IEvent

type internal ComponentEvent<'T> =
    | Added of Entity * 'T
    | Removed of Entity * 'T

    interface IEvent

type IComponentQuery =

    abstract Has<'T when 'T :> IComponent<'T>> : Entity -> bool

    abstract TryGet<'T when 'T :> IComponent<'T>> : Entity -> 'T option

    abstract TryFind<'T when 'T :> IComponent<'T>> : (Entity * 'T -> bool) -> (Entity * 'T) option

    abstract Get<'T when 'T :> IComponent<'T>> : unit -> (Entity * 'T) []

    abstract Get<'T1, 'T2 when 'T1 :> IComponent<'T1> and 'T2 :> IComponent<'T2>> : unit -> (Entity * 'T1 * 'T2) []

    abstract ForEach<'T when 'T :> IComponent<'T>> : (Entity * 'T -> unit) -> unit

    abstract ForEach<'T1, 'T2 when 'T1 :> IComponent<'T1> and 'T2 :> IComponent<'T2>> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent<'T1> and 'T2 :> IComponent<'T2> and 'T3 :> IComponent<'T3>> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent<'T1> and 'T2 :> IComponent<'T2> and 'T3 :> IComponent<'T3> and 'T4 :> IComponent<'T4>> : (Entity * 'T1 * 'T2 * 'T3 * 'T4 -> unit) -> unit

    abstract ParallelForEach<'T when 'T :> IComponent<'T>> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2 when 'T1 :> IComponent<'T1> and 'T2 :> IComponent<'T2>> : (Entity * 'T1 * 'T2 -> unit) -> unit

type IComponentService =

    abstract Add<'T when 'T :> IComponent<'T>> : Entity -> 'T -> unit

    abstract Remove<'T when 'T :> IComponent<'T>> : Entity -> unit

type IEntityService =

    abstract Create : id: int -> unit

    abstract Destroy : Entity -> unit

[<Sealed>]
type internal EntityManager =
    
    interface IComponentQuery

    interface IComponentService

    interface IEntityService

    member Process : unit -> unit

    new : IEventAggregator * entityAmount: int -> EntityManager