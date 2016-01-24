namespace ECS.Core

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

[<Sealed>]
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

[<Sealed>]
type EventListener<'Event when 'Event :> IEvent> (f) =

    interface ISystem with

        member __.Init (entities, events) =
            events.GetEvent<'Event> ()
            |> Observable.add (fun event -> f entities event)

        member __.Update (_, _) = ()

[<Sealed>]
type EventQueue<'Event when 'Event :> IEvent> (f) =
    let queue = System.Collections.Concurrent.ConcurrentQueue<'Event> ()

    interface ISystem with

        member __.Init (_, events) =
            events.GetEvent<'Event> ()
            |> Observable.add queue.Enqueue

        member __.Update (entities, events) =
            let mutable event = Unchecked.defaultof<'Event>
            while queue.TryDequeue (&event) do
                f entities event