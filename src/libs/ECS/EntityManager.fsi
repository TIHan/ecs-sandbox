namespace ECS.Core

open System

type IComponent = interface end

[<Sealed>]
type ComponentAdded<'T when 'T :> IComponent> =

    member Entity : Entity

    member Component : 'T

    interface IEvent

[<Sealed>]
type ComponentRemoved<'T when 'T :> IComponent> =

    member Entity : Entity

    member Component : 'T

    interface IEvent

[<Sealed>]
type AnyComponentAdded =

    member Entity : Entity

    member Component : IComponent

    member ComponentType : Type

    interface IEvent

[<Sealed>]
type AnyComponentRemoved =

    member Entity : Entity

    member Component : IComponent

    member ComponentType : Type

    interface IEvent

[<Sealed>]
type EntitySpawned =

    member Entity : Entity

    interface IEvent

[<Sealed>]
type EntityDestroyed =

    member Entity : Entity

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

    member internal Spawn : Entity -> unit

    member Destroy : Entity -> unit

    member internal Process : unit -> unit

    new : EventAggregator * entityAmount: int -> EntityManager