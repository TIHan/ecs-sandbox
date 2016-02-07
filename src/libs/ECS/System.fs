namespace ECS

type Entities = EntityManager
type Events = EventAggregator

type ISystem =

    abstract Init : Entities * Events -> unit

    abstract Update : Entities * Events -> unit

module Systems =

    [<Sealed>]
    type EventQueue<'Event when 'Event :> IEvent> (f) =
        let queue = System.Collections.Concurrent.ConcurrentQueue<'Event> ()

        interface ISystem with

            member __.Init (_, events) =
                events.GetEvent<'Event> ()
                |> Event.add queue.Enqueue

            member __.Update (entities, events) =
                let mutable event = Unchecked.defaultof<'Event>
                while queue.TryDequeue (&event) do
                    f entities events event


        static member Create f = EventQueue (f)

    [<Sealed>]
    type EntityProcessor () =

        interface ISystem with

            member __.Init (_, _) = ()

            member __.Update (entities, _) = entities.Process ()

        static member Create () = EntityProcessor ()