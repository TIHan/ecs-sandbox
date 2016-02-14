namespace ECS

type IECSEvent = interface end

[<Sealed>]
type EventManager =

    member Publish : #IECSEvent -> unit

    member internal GetEvent<'T when 'T :> IECSEvent> : unit -> Event<'T>

    internal new : unit -> EventManager

type Events = EventManager
