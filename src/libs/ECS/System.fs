namespace ECS

type SystemUpdate = SystemUpdate of (Entities -> Events -> unit)

[<AbstractClass>]
type HandleEvent () = 

    abstract Handle : Entities * Events -> unit

type HandleEvent<'T when 'T :> IECSEvent> (f: Entities -> 'T -> unit) =
    inherit HandleEvent ()

    override this.Handle (entities, events) = 
        events.GetEvent<'T>().Publish.Add (fun event -> f entities event)

type IECSSystem =

    abstract Init : HandleEvent list * SystemUpdate

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System (name: string, handleEvents, update) =

        member this.Name = name

        interface IECSSystem with

            member __.Init = (handleEvents, SystemUpdate update)

    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> (f) =

        interface IECSSystem with

            member __.Init =
                let queue = System.Collections.Concurrent.ConcurrentQueue<'T> ()
                (
                    [
                        HandleEvent<'T> (fun _ -> queue.Enqueue)
                    ],
                    SystemUpdate (fun entities _ ->
                        let mutable event = Unchecked.defaultof<'T>
                        while queue.TryDequeue (&event) do
                            f entities event
                    )
                )

    [<Sealed>]
    type EntityProcessor () =

        interface IECSSystem with

            member __.Init =
                (
                    [],
                    SystemUpdate (fun entities _ -> entities.Process ())
                )