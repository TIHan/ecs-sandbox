namespace ECS.Core

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

[<Sealed>]
type EventSystem<'Event when 'Event :> IEvent> (f) =

    interface ISystem with

        member __.Init (entities, events) =
            events.GetEvent<'Event> ()
            |> Observable.add (fun event -> f entities event)

        member __.Update (_, _) = ()

[<Sealed>]
type EventQueueSystem<'Event when 'Event :> IEvent> (f) =
    let queue = System.Collections.Concurrent.ConcurrentQueue<'Event> ()

    interface ISystem with

        member __.Init (_, events) =
            events.GetEvent<'Event> ()
            |> Observable.add queue.Enqueue

        member __.Update (entities, events) =
            let mutable event = Unchecked.defaultof<'Event>
            while queue.TryDequeue (&event) do
                f entities event

[<Sealed>]
type EntityProcessorSystem () =

    interface ISystem with

        member __.Init (_, _) = ()

        member __.Update (entities, _) = entities.Process ()