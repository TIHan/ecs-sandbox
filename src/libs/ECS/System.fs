namespace ECS

type Entities = EntityManager

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
    type EventQueue<'T when 'T :> IECSEvent> (f) =

        interface ISystem with

            member __.Init (entities, events) =
                let queue = System.Collections.Concurrent.ConcurrentQueue<'T> ()

                events.Listen queue.Enqueue

                SystemUpdate (fun () ->
                    let mutable event = Unchecked.defaultof<'T>
                    while queue.TryDequeue (&event) do
                        f entities event
                )

    [<Sealed>]
    type EntityProcessor () =

        interface ISystem with

            member __.Init (entities, _) =
                SystemUpdate entities.Process