namespace ECS.Core

open System

type IEvent = interface end

[<Sealed>]
type EventAggregator =

    internal new : unit -> EventAggregator

    member internal GetEvent<'T when 'T :> IEvent> : unit -> IObservable<'T>

    member Publish : #IEvent -> unit