namespace ECS.Core

open System

open ECS.Core

type ISystem<'T> =

    abstract Init : World<'T> -> unit

    abstract Update : World<'T> -> unit

and [<Sealed>] World<'U> =

    new : 'U * int * ISystem<'U> list -> World<'U>
   
    member Run : unit -> unit

    member Dependency : 'U

    member EventAggregator : IEventAggregator

    member ComponentQuery : IComponentQuery

    member ComponentService : IComponentService

    member EntityService : IEntityService

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =

    val spawned : World<_> -> IObservable<Entity>

    val destroyed : World<_> -> IObservable<Entity>

module Component =

    val anyAdded : World<_> -> IObservable<Entity * IComponent * Type>

    val anyRemoved : World<_> -> IObservable<Entity * IComponent * Type>

    val added : World<_> -> IObservable<Entity * #IComponent>

    val removed : World<_> -> IObservable<Entity * #IComponent>

[<Sealed>]
type EntityBlueprint

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityBlueprint =

    val create : unit -> EntityBlueprint

    val add<'T when 'T :> IComponent> : (unit -> 'T) -> EntityBlueprint -> EntityBlueprint

    val spawn : int -> World<_> -> EntityBlueprint -> unit
