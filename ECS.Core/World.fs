namespace ECS.Core

open System
open System.Diagnostics
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks

[<Sealed>]
type WorldTime () =

    member val Current = Var.create TimeSpan.Zero with get

    member val Interval = Var.create TimeSpan.Zero with get

    member val Delta = Var.create 0.f with get

type ISystem =

    abstract Init : WorldTime -> IEventAggregator -> IEntityFactory -> IComponentQuery -> unit

    abstract Update : WorldTime -> IEventAggregator -> IEntityFactory -> IComponentQuery -> unit

[<Sealed>]
type World (entityAmount, systems: ISystem list) =
    let eventAggregator = EventAggregator () :> IEventAggregator
    let entityManager = EntityManager (eventAggregator, entityAmount)
    let entityFactory = entityManager :> IEntityFactory
    let componentQuery = entityManager :> IComponentQuery
    let time = WorldTime ()

    do
        systems
        |> List.iter (fun system -> system.Init time eventAggregator entityFactory componentQuery)

    member __.Time = time

    member __.EventAggregator = eventAggregator

    member __.EntityFactory = entityFactory

    member __.ComponentQuery = componentQuery

    member __.Run () =
        entityManager.Process ()

        systems |> List.iter (fun (sys: ISystem) ->
            sys.Update time eventAggregator entityFactory componentQuery
            entityManager.Process ()
        )
