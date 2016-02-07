namespace ECS

type IEvent = interface end

[<Sealed>]
type EventAggregator =

    internal new : unit -> EventAggregator

    member GetEvent<'T when 'T :> IEvent> : unit -> IEvent<'T>

    member Publish : #IEvent -> unit