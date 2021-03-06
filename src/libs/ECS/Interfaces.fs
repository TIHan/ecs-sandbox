﻿namespace ECS.Core

open System

type IEventData = interface end

type IEventAggregator =

    abstract GetEvent : unit -> IObservable<#IEventData>

    abstract Publish : #IEventData -> unit

type IComponent = interface end

type IComponentQuery =

    abstract Has<'T when 'T :> IComponent> : Entity -> bool

    abstract TryGet : Entity * Type -> IComponent option

    abstract TryGet : Entity * byref<#IComponent> -> unit

    abstract TryGet<'T when 'T :> IComponent> : Entity -> 'T option

    abstract TryFind<'T when 'T :> IComponent> : (Entity -> 'T -> bool) -> (Entity * 'T) option

    abstract GetAll<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract GetAll<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []

    abstract ForEach<'T when 'T :> IComponent> : (Entity -> 'T -> unit) -> unit

    abstract ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit) -> unit

    abstract ParallelForEach<'T when 'T :> IComponent> : (Entity -> 'T -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

type IComponentService =

    abstract Add<'T when 'T :> IComponent> : Entity -> 'T -> unit

    abstract Remove<'T when 'T :> IComponent> : Entity -> unit

    abstract GetAddedEvent<'T when 'T :> IComponent> : unit -> IObservable<Entity * 'T>

    abstract GetAnyAddedEvent : unit -> IObservable<Entity * IComponent * Type>

    abstract GetRemovedEvent<'T when 'T :> IComponent> : unit -> IObservable<Entity * 'T>

    abstract GetAnyRemovedEvent : unit -> IObservable<Entity * IComponent * Type>

type IEntityService =

    abstract Spawn : Entity -> unit

    abstract Destroy : Entity -> unit

    abstract GetSpawnedEvent : unit -> IObservable<Entity>

    abstract GetDestroyedEvent : unit -> IObservable<Entity>
