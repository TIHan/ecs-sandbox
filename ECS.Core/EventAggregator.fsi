namespace ECS.Core

open System

type IEvent = interface end

type IEventAggregator =

    abstract GetEvent<'T when 'T :> IEvent> : unit -> IObservable<'T>

    abstract Publish<'T when 'T :> IEvent> : 'T -> unit

[<Sealed>]
type internal EventAggregator =

    interface IEventAggregator

    new : unit -> EventAggregator
