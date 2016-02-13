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
    type EventQueue<'T, 'U when 'T :> IECSEvent<'U>> (f) =

        interface ISystem with

            member __.Init (entities, events) =
                let queue = System.Collections.Concurrent.ConcurrentQueue<'U> ()

                events.Listen queue.Enqueue

                SystemUpdate (fun () ->
                    let mutable event = Unchecked.defaultof<'U>
                    while queue.TryDequeue (&event) do
                        f entities events event
                )

    [<Sealed>]
    type EntityProcessor () =

        interface ISystem with

            member __.Init (entities, _) =
                SystemUpdate entities.Process