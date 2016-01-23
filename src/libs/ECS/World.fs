namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

type System private () =

    static member Append (sys1: ISystem) (sys2: ISystem) =
        {
            new ISystem with

                member __.Init (entities, events) =
                    sys1.Init (entities, events)
                    sys2.Init (entities, events)

                member __.Update (entities, events) =
                    sys1.Update (entities, events)
                    sys2.Update (entities, events)
        }

    static member Empty =
        {
            new ISystem with

                member __.Init (_, _) = ()

                member __.Update (_, _) = ()
        }

type [<Sealed>] World (entityAmount, systems: ISystem list) =
    let eventAggregator = EventAggregator ()
    let entityManager = EntityManager (eventAggregator, entityAmount)
    let deps = (entityManager, eventAggregator)

    do
        systems
        |> List.iter (fun system -> system.Init deps)

    member __.Run () =
        systems |> List.iter (fun sys ->
            sys.Update deps
        )

    member __.Events = eventAggregator

    member __.Entities = entityManager

type EventSystem<'Event when 'Event :> IEvent> (update) =
    let queue = System.Collections.Concurrent.ConcurrentQueue<'Event> ()

    interface ISystem with

        member __.Init (_, events) =
            events.GetEvent<'Event> ()
            |> Observable.add queue.Enqueue

        member __.Update (entities, events) =
            let mutable eventValue = Unchecked.defaultof<'Event>
            while queue.TryDequeue (&eventValue) do
                update entities eventValue
