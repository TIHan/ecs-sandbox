namespace ECS.Core

open System
open System.Diagnostics
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks

[<Sealed>]
type World (entityAmount) =
    let eventAggregator : IEventAggregator = EventAggregator () :> IEventAggregator
    let entityManager = EntityManager (eventAggregator, entityAmount)
    let systems = ResizeArray ()
    let deferQueue = MessageQueue<unit -> unit> ()

    member inline this.Defer f =
        deferQueue.Push f

    member val Time = TimeSpan.Zero with get, set

    member val Interval = TimeSpan.Zero with get, set

    member val Delta = 0.f with get, set

    member this.Run () =
        deferQueue.Process (fun f -> f ())
        (entityManager :> IEntityFactory).Process ()

        systems.ForEach (fun (sys: ISystem) ->
            sys.Update this
        )

    member this.EntityQuery = entityManager :> IEntityQuery

    member this.EntityFactory = entityManager :> IEntityFactory

    member this.EventAggregator = eventAggregator

    member this.AddSystem (system: ISystem) : unit =
        let inline f () =
            systems.Add system
            system.Init this

        this.Defer f

and ISystem =

    abstract Init : World -> unit

    abstract Update : World -> unit
