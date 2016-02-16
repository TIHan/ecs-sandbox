namespace ECS

[<AbstractClass>]
type HandleEvent () = 

    abstract Handle : Entities -> Events -> unit

type HandleEvent<'T when 'T :> IECSEvent> (f: Entities -> 'T -> unit) =
    inherit HandleEvent ()

    override this.Handle entities events = 
        let handle = f entities
        events.GetEvent<'T>().Publish.Add handle

type IECSSystem<'UpdateData> =

    abstract HandleEvents : HandleEvent list

    abstract Init : Entities -> Events -> ('UpdateData -> unit)

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System<'UpdateData> (name: string, handleEvents, init) =

        member this.Name = name

        interface IECSSystem<'UpdateData> with

            member __.HandleEvents = handleEvents

            member __.Init entities events =
                init entities events

    [<Sealed>]
    type EventQueue<'UpdateData, 'Event when 'Event :> IECSEvent> (f) =
        let queue = System.Collections.Concurrent.ConcurrentQueue<'Event> ()

        interface IECSSystem<'UpdateData> with

            member __.HandleEvents =
                [
                    HandleEvent<'Event> (fun _ -> queue.Enqueue)
                ]

            member __.Init entities _ =
                let f = f entities
                fun t ->
                    let mutable event = Unchecked.defaultof<'Event>
                    while queue.TryDequeue (&event) do
                        f t event

    [<Sealed>]
    type EntityProcessor () =

        interface IECSSystem<unit> with

            member __.HandleEvents = []

            member __.Init entities _ = entities.Process