namespace ECS

/// A marker for event data.
type IECSEvent = interface end

/// Responsible for publishing events.
[<Sealed>]
type EventManager =

    static member internal Create : unit -> EventManager

    /// Publishes an event to underlying subscribers.
    member Publish : #IECSEvent -> unit

    member internal GetEvent<'T when 'T :> IECSEvent> : unit -> Event<'T>

/// Responsible for publishing events.
type Events = EventManager
