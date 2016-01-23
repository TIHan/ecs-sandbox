namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

type [<Sealed>] World =

    new : int * ISystem list -> World
   
    member Run : unit -> unit

    member Events : EventAggregator

    member Entities : EntityManager
