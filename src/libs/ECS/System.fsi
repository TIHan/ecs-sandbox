namespace BeyondGames.Ecs

open System

/// A base handle to an event.
[<AbstractClass>]
type EntityEvent =

    abstract internal Handle : Entities -> Events -> IDisposable

/// A handle to an event.
[<Sealed; Class>]
type EntityEvent<'T when 'T :> IEntityEvent> =
    inherit EntityEvent

[<AutoOpen>]
module EntityEventOperators =

    val handle<'T when 'T :> IEntityEvent> : (Entities -> 'T -> unit) -> EntityEvent<'T>

type IEntitySystemShutdown =

    abstract Shutdown : unit -> unit

/// Behavior that processes entities and the entities' components.
/// The init method returns a function; that function is what gets called to update the system.
type IEntitySystem<'UpdateData> =

    abstract Events : EntityEvent list

    abstract Initialize : Entities -> Events -> ('UpdateData -> unit)

[<RequireQualifiedAccess>]
module EntitySystems =

    /// Basic system.
    [<Sealed>]
    type EntitySystem<'UpdateData> =

        member Name : string

        interface IEntitySystem<'UpdateData>

        new : string * EntityEvent list * (Entities -> Events -> 'UpdateData -> unit) -> EntitySystem<'UpdateData>

    /// Processes entities to see if any need to be destroyed/spawned and components added/removed.
    [<Sealed>]
    type EntityProcessor =

        interface IEntitySystem<unit>

        new : unit -> EntityProcessor
