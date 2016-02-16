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
type IECSSystem =

    abstract HandleEvents : HandleEvent list

    abstract Update : Entities -> Events -> unit

/// Behavior that processes entities and the entities' components.
type IECSSystem<'D1> =
   
    abstract HandleEvents : HandleEvent list

    abstract Update : Entities -> Events -> 'D1 -> unit

[<RequireQualifiedAccess>]
module Systems =

    /// Basic, non-typed system.
    [<Sealed>]
    type System =

        interface IECSSystem

        new : string * HandleEvent list * (Entities -> Events -> unit) -> System

    /// Basic, non-typed system.
    [<Sealed>]
    type System<'D1> =

        interface IECSSystem<'D1>

        new : string * HandleEvent list * (Entities -> Events -> 'D1 -> unit) -> System<'D1>

    /// Queues the specified event type by listening to it. When update is called, it calls the lambda passed through the constructor.
    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> =

        interface IECSSystem

        new : (Entities -> 'T -> unit) -> EventQueue<'T>

    /// Queues the specified event type by listening to it. When update is called, it calls the lambda passed through the constructor.
    [<Sealed>]
    type EventQueue<'T, 'D1 when 'T :> IECSEvent> =

        interface IECSSystem<'D1>

        new : (Entities -> 'D1 -> 'T -> unit) -> EventQueue<'T, 'D1>

    /// Processes entities to see if any need to be destroyed/spawned and components added/removed.
    [<Sealed>]
    type EntityProcessor =

        interface IECSSystem

        new : unit -> EntityProcessor
