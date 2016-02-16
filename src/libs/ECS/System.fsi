namespace ECS

/// A base handle to an event.
[<AbstractClass>]
type HandleEvent =

    abstract internal Handle : Entities -> Events -> unit

/// A handle to an event.
type HandleEvent<'T when 'T :> IECSEvent> =
    inherit HandleEvent

    new : (Entities -> 'T -> unit) -> HandleEvent<'T>

/// Behavior that processes entities and the entities' components.
/// The init method returns a function; that function is what gets called to update the system.
type IECSSystem<'UpdateData> =

    abstract HandleEvents : HandleEvent list

    abstract Init : Entities -> Events -> ('UpdateData -> unit)

[<RequireQualifiedAccess>]
module Systems =

    /// Basic system.
    [<Sealed>]
    type System<'UpdateData> =

        member Name : string

        interface IECSSystem<'UpdateData>

        new : string * HandleEvent list * (Entities -> Events -> 'UpdateData -> unit) -> System<'UpdateData>

    /// Queues the specified event type by listening to it. When update is called, it calls the lambda passed through the constructor.
    [<Sealed>]
    type EventQueue<'UpdateData, 'Event when 'Event :> IECSEvent> =

        interface IECSSystem<'UpdateData>

        new : (Entities -> 'UpdateData -> 'Event -> unit) -> EventQueue<'UpdateData, 'Event>

    /// Processes entities to see if any need to be destroyed/spawned and components added/removed.
    [<Sealed>]
    type EntityProcessor =

        interface IECSSystem<unit>

        new : unit -> EntityProcessor
