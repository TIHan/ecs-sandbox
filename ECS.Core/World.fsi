namespace ECS.Core

open System

[<Sealed>]
type WorldTime =

    member Current : Var<TimeSpan>

    member Interval : Var<TimeSpan>

    member Delta : Var<single>

type ISystem =

    abstract Init : WorldTime -> IEventAggregator -> IEntityFactory -> IComponentQuery -> unit

    abstract Update : WorldTime -> IEventAggregator -> IEntityFactory -> IComponentQuery -> unit

[<Sealed>]
type World =

    new : int * ISystem list -> World

    member Time : WorldTime

    member EventAggregator : IEventAggregator

    member EntityFactory : IEntityFactory

    member ComponentQuery : IComponentQuery

    member Run : unit -> unit
