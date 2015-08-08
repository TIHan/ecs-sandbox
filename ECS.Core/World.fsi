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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module World =

    val entitySpawned : IWorld -> IObservable<Entity>

    val entityDestroyed : IWorld -> IObservable<Entity>

    val componentAdded<'T when 'T :> IComponent> : IWorld -> IObservable<Entity * 'T>

    val componentRemoved<'T when 'T :> IComponent> : IWorld -> IObservable<Entity * 'T>

[<Sealed>]
type EntityDescription

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =

    val create : int -> EntityDescription

    val add<'T when 'T :> IComponent> : 'T -> EntityDescription -> EntityDescription

    val remove<'T when 'T :> IComponent> : EntityDescription -> EntityDescription

    val run : IWorld -> EntityDescription -> unit
