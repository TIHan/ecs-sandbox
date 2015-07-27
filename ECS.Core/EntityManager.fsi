namespace ECS.Core

open System

type EntityEvent =
    | CreatedActive of Entity
    | CreatedInactive of Entity
    | Activated of Entity
    | Deactivated of Entity
    | Destroyed of Entity
    | ComponentAdded of Entity * Type * obj
    | ComponentRemoved of Entity * Type * obj

type IEntityQuery =

    abstract HasComponent<'T when 'T :> IComponent> : Entity -> bool

    abstract TryGetComponent<'T when 'T :> IComponent> : Entity -> 'T option

    abstract IsActive : Entity -> bool

    abstract ForEachActive : (Entity -> unit) -> unit


    abstract GetActiveComponents<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract GetActiveComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetInactiveComponents<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract GetInactiveComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetComponents<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract GetComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []


    abstract ForEachActiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEachActiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ForEachActiveComponent<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit


    abstract ForEachInactiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEachInactiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ForEachComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEachComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    
    abstract ParallelForEachActiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachActiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachInactiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachInactiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

type IEntityFactory =

    abstract CreateInactive : id: int -> IComponent list -> unit

    abstract CreateActive : id: int -> IComponent list -> unit

    abstract Activate : Entity -> unit

    abstract Deactivate : Entity -> unit

    abstract Destroy : Entity -> unit

    abstract AddComponent<'T when 'T :> IComponent> : Entity -> 'T -> unit

    abstract RemoveComponent<'T when 'T :> IComponent> : Entity -> unit

    abstract Process : unit -> unit

[<Sealed>]
type internal EntityManager =

    interface IEntityFactory
    
    interface IEntityQuery

    new : IEventAggregator * entityAmount: int -> EntityManager