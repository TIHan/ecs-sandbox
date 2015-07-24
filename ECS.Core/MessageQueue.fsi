namespace ECS.Core

[<Sealed>]
type internal MessageQueue<'T> =

    member Push : 'T -> unit

    member TryPop : unit -> 'T option

    member Process : ('T -> unit) -> unit

    new : unit -> MessageQueue<'T>