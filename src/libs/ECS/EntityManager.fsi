namespace ECS.Core

open System

[<Sealed>]
type internal EntityManager =
    
    interface IComponentQuery

    interface IComponentService

    interface IEntityService

    member Process : unit -> unit

    new : entityAmount: int -> EntityManager