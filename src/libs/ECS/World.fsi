namespace ECS.Core

open System

[<Sealed>]
type WorldTime =

    member Current : Var<TimeSpan>

    member Interval : Var<TimeSpan>

    member Delta : Var<single>

type ISystem =

    abstract Init : IWorld -> unit

    abstract Update : IWorld -> unit

and IWorld =

    abstract Time : WorldTime

    abstract EventAggregator : IEventAggregator

    abstract ComponentQuery : IComponentQuery

    abstract ComponentService : IComponentService

    abstract EntityService : IEntityService

[<Sealed>]
type World =

    new : int * ISystem list -> World
   
    member Run : unit -> unit

    interface IWorld

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module World =

    val event : IWorld -> IObservable<#IEvent>

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =

    val spawned : IWorld -> IObservable<Entity>

    val destroyed : IWorld -> IObservable<Entity>

    val anyComponentAdded : IWorld -> IObservable<Entity * obj * Type>

    val anyComponentRemoved : IWorld -> IObservable<Entity * obj * Type>

    val componentAdded<'T when 'T :> IComponent> : IWorld -> IObservable<Entity * 'T>

    val componentRemoved<'T when 'T :> IComponent> : IWorld -> IObservable<Entity * 'T>

    val addComponent<'T when 'T :> IComponent> : Entity -> 'T -> IWorld -> unit

    val removeComponent<'T when 'T :> IComponent> : Entity -> IWorld -> unit

[<Sealed>]
type EntityBlueprint

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityBlueprint =

    val create : unit -> EntityBlueprint

    val add<'T when 'T :> IComponent> : 'T -> EntityBlueprint -> EntityBlueprint

    val remove<'T when 'T :> IComponent> : EntityBlueprint -> EntityBlueprint

    val build : IWorld -> EntityBlueprint -> unit
