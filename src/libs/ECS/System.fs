namespace ECS

type Entities = EntityManager
type Events = EventAggregator

type SystemUpdate = SystemUpdate of (unit -> unit)

type ISystem =

    abstract Init : Entities * Events -> SystemUpdate

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System (name: string, f) =

        member this.Name = name

        interface ISystem with

            member __.Init (entities, events) =
                f entities events

    [<Sealed>]
    type EventQueue<'Event when 'Event :> IEvent> (f) =

        interface ISystem with

            member __.Init (entities, events) =
                let queue = System.Collections.Concurrent.ConcurrentQueue<'Event> ()

                events.Listen queue.Enqueue

                SystemUpdate (fun () ->
                    let mutable event = Unchecked.defaultof<'Event>
                    while queue.TryDequeue (&event) do
                        f entities events event
                )

    [<Sealed>]
    type EntityProcessor () =

        interface ISystem with

            member __.Init (entities, _) =
                SystemUpdate entities.Process