namespace ECS.Core

open System
open System.Diagnostics
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks

[<Sealed>]
type World (entityAmount) =
    let eventAggregator = EventAggregator () :> IEventAggregator
    let entityManager = EntityManager (eventAggregator, entityAmount)
    let entityFactory = entityManager :> IEntityFactory
    let componentQuery = entityManager :> IComponentQuery
    let systems = ResizeArray ()
    let deferQueue = MessageQueue<unit -> unit> ()

    member inline this.Defer f =
        deferQueue.Push f

    member val Time = Var.create TimeSpan.Zero with get

    member val Interval = TimeSpan.Zero with get, set

    member val Delta = 0.f with get, set

    member this.Run () =
        deferQueue.Process (fun f -> f ())

        systems |> Seq.iter (fun (sys: ISystem) ->
            sys.Update this
            entityFactory.Process ()
        )

    member this.ComponentQuery = componentQuery

    member this.EntityFactory = entityFactory

    member this.EventAggregator = eventAggregator

    member this.AddSystem (system: ISystem) : unit =
        let inline f () =
            systems.Add system
            system.Init this

        this.Defer f

and ISystem =

    abstract Init : World -> unit

    abstract Update : World -> unit
