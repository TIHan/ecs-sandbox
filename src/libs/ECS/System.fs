namespace ECS

[<AbstractClass>]
type HandleEvent () = 

    abstract Handle : Entities * Events -> unit

type HandleEvent<'T when 'T :> IECSEvent> (f: Entities -> Events -> 'T -> unit) =
    inherit HandleEvent ()

    override this.Handle (entities, events) = 
        events.GetEvent<'T>().Publish.Add (fun event -> f entities events event)

type IECSSystem =

    abstract HandleEvents : HandleEvent list

    abstract Update : Entities * Events -> unit

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System (name: string, handleEvents, update) =

        member this.Name = name

        interface IECSSystem with

            member __.HandleEvents = handleEvents

            member __.Update (entities, events) =
                update entities events

    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> (f) =
        let queue = System.Collections.Concurrent.ConcurrentQueue<'T> ()

        interface IECSSystem with

            member __.HandleEvents =
                [
                    HandleEvent<'T> (fun _ _ -> queue.Enqueue)
                ]

            member __.Update (entities, events) =
                let mutable event = Unchecked.defaultof<'T>
                while queue.TryDequeue (&event) do
                    f entities events event

    [<Sealed>]
    type EntityProcessor () =

        interface IECSSystem with

            member __.HandleEvents = []

            member __.Update (entities, _) = entities.Process ()