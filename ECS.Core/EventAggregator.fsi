namespace ECS.Core

open System

type IEventAggregator =

    abstract GetEvent : unit -> IObservable<'T>

    abstract Publish : 'T -> unit

[<Sealed>]
type internal EventAggregator =

    interface IEventAggregator

    new : unit -> EventAggregator
