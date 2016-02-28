namespace BeyondGames.Ecs

/// A marker for event data.
type IEntityEvent = interface end

/// Responsible for publishing events.
[<Sealed>]
type EventManager =

    static member internal Create : unit -> EventManager

    /// Publishes an event to underlying subscribers.
    member Publish : #IEntityEvent -> unit

    member internal GetEvent<'T when 'T :> IEntityEvent> : unit -> Event<'T>

/// Responsible for publishing events.
type Events = EventManager
