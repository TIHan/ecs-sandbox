namespace ECS.Core

open System

open ECS.Core

[<Sealed>]
type ECSWorld<'U> =

    new : 'U * int * ISystem<'U> list -> ECSWorld<'U>
   
    member Run : unit -> unit

    interface IWorld<'U>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =

    val spawned : World<_, IObservable<Entity>>

    val destroyed : World<_, IObservable<Entity>>

module Component =

    val anyAdded : World<_, IObservable<Entity * IComponent * Type>>

    val anyRemoved : World<_, IObservable<Entity * IComponent * Type>>

    val added : World<_, IObservable<Entity * #IComponent>>

    val removed : World<_, IObservable<Entity * #IComponent>>

[<Sealed>]
type EntityBlueprint

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityBlueprint =

    val create : unit -> EntityBlueprint

    val add<'T when 'T :> IComponent> : (unit -> 'T) -> EntityBlueprint -> EntityBlueprint

    val spawn : int -> IWorld<_> -> EntityBlueprint -> unit
