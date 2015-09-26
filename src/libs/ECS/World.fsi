namespace ECS.Core

open System

open ECS.Core

[<Sealed>]
type ECSWorld =

    new : int * ISystem list -> ECSWorld
   
    member Run : unit -> unit

    interface IWorld

module World =

    val event : IWorld -> IObservable<#IEventData>

    module Entity =

        val spawned : IWorld -> IObservable<Entity>

        val destroyed : IWorld -> IObservable<Entity>

    module Component =

        val anyAdded : IWorld -> IObservable<Entity * IComponent * Type>

        val anyRemoved : IWorld -> IObservable<Entity * IComponent * Type>

        val added<'T when 'T :> IComponent> : IWorld -> IObservable<Entity * 'T>

        val removed<'T when 'T :> IComponent> : IWorld -> IObservable<Entity * 'T>

[<Sealed>]
type EntityBlueprint

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityBlueprint =

    val create : unit -> EntityBlueprint

    val add<'T when 'T :> IComponent> : (unit -> 'T) -> EntityBlueprint -> EntityBlueprint

    val spawn : int -> IWorld -> EntityBlueprint -> unit
