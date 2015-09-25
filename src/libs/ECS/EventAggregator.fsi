namespace ECS.Core

open System

[<Sealed>]
type internal EventAggregator =

    interface IEventAggregator

    new : unit -> EventAggregator
