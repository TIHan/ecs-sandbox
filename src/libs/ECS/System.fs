namespace BeyondGames.Ecs

open System

[<AbstractClass>]
type EntityEvent () = 

    abstract Handle : Entities -> Events -> IDisposable

[<Sealed>]
type EntityEvent<'T when 'T :> IEntityEvent> (f: Entities -> 'T -> unit) =
    inherit EntityEvent ()

    override this.Handle entities events = 
        let handle = f entities
        events.GetEvent<'T>().Publish.Subscribe handle

[<AutoOpen>]
module EntityEventOperators =

    let handle<'T when 'T :> IEntityEvent> = EntityEvent<'T>

type IEntitySystemShutdown =

    abstract Shutdown : unit -> unit

type IEntitySystem<'UpdateData> =

    abstract Events : EntityEvent list

    abstract Initialize : Entities -> Events -> ('UpdateData -> unit)

[<RequireQualifiedAccess>]
module EntitySystems =

    [<Sealed>]
    type EntitySystem<'UpdateData> (name: string, events, init) =

        member this.Name = name

        interface IEntitySystem<'UpdateData> with

            member __.Events = events

            member __.Initialize entities events =
                init entities events

    [<Sealed>]
    type EntityProcessor () =

        interface IEntitySystem<unit> with

            member __.Events = []

            member __.Initialize entities _ = entities.Process