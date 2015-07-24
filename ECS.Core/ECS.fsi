namespace ECS.Core

open System

[<Sealed>]
type World =

    new : int -> World

    member Time : TimeSpan with get, set

    member Interval : TimeSpan with get, set

    member Delta : single with get, set

    member Run : unit -> unit

    member EntityQuery : IEntityQuery

    member EntityFactory : IEntityFactory

    member EventAggregator : IEventAggregator

    member AddSystem : ISystem -> unit

and ISystem =

    abstract Init :  World -> unit

    abstract Update : World -> unit

