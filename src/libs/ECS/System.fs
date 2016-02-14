namespace ECS

type SystemUpdate = SystemUpdate of (unit -> unit)

[<AbstractClass>]
type HandleEvent () = 

    abstract Handle : Entities * Events -> unit

type HandleEvent<'T when 'T :> IECSEvent> (f: Entities -> 'T -> unit) =
    inherit HandleEvent ()

    override this.Handle (entities, events) = 
        let event = events.GetEvent<'T> ()
        event.Publish.Add (fun eventValue -> f entities eventValue)

type IECSSystem =

    abstract HandleEvents : HandleEvent list

    abstract Init : Entities * Events -> SystemUpdate

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System (name: string, handleEvents, init) =

        member this.Name = name

        interface IECSSystem with

            member __.HandleEvents = handleEvents

            member __.Init (entities, events) =
                init entities events

    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> (f) =
        let queue = System.Collections.Concurrent.ConcurrentQueue<'T> ()

        interface IECSSystem with

            member __.HandleEvents =
                [
                    HandleEvent<'T> (fun _ -> queue.Enqueue)
                ]

            member __.Init (entities, events) =
                SystemUpdate (fun () ->
                    let mutable event = Unchecked.defaultof<'T>
                    while queue.TryDequeue (&event) do
                        f entities event
                )

    [<Sealed>]
    type EntityProcessor () =

        interface IECSSystem with

            member __.HandleEvents = []

            member __.Init (entities, _) =
                SystemUpdate entities.Process