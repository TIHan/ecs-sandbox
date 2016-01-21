namespace ECS.Core

open System

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

    member AddComponent<'T when 'T :> IComponent> : Entity -> 'T -> unit

    member RemoveComponent<'T when 'T :> IComponent> : Entity -> unit

    member GetAddedEvent<'T when 'T :> IComponent> : unit -> IObservable<Entity * 'T>

    member GetAnyAddedEvent : unit -> IObservable<Entity * IComponent * Type>

    member GetRemovedEvent<'T when 'T :> IComponent> : unit -> IObservable<Entity * 'T>

    member GetAnyRemovedEvent : unit -> IObservable<Entity * IComponent * Type>

    // Entites

    member Spawn : Entity -> unit

    member Destroy : Entity -> unit

    member GetSpawnedEvent : unit -> IObservable<Entity>

    member GetDestroyedEvent : unit -> IObservable<Entity>

    member Process : unit -> unit

    new : entityAmount: int -> EntityManager