namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : World -> unit

    abstract Update : World -> unit

and [<Sealed>] World =

    new : int * ISystem list -> World
   
    member Run : unit -> unit

    member EventAggregator : IEventAggregator

    member ComponentQuery : IComponentQuery

    member ComponentService : IComponentService

    member EntityService : IEntityService

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =

    val onSpawned : World -> IObservable<Entity>

    val onDestroyed : World -> IObservable<Entity>

module Component =

    val onAnyAdded : World -> IObservable<Entity * IComponent * Type>

    val onAnyRemoved : World -> IObservable<Entity * IComponent * Type>

    val onAdded : World -> IObservable<Entity * #IComponent>

    val onRemoved : World -> IObservable<Entity * #IComponent>

[<Sealed>]
type EntityBlueprint

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityBlueprint =

    val create : unit -> EntityBlueprint

    val add<'T when 'T :> IComponent> : (unit -> 'T) -> EntityBlueprint -> EntityBlueprint

    val spawn : int -> World -> EntityBlueprint -> unit
