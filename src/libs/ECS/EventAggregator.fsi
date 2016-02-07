namespace ECS

type IEvent = interface end

[<Sealed>]
type EventAggregator =

    internal new : unit -> EventAggregator

    member Listen : (#IEvent -> unit) -> unit

    member Publish : #IEvent -> unit