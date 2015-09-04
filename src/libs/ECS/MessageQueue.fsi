﻿namespace ECS.Core

[<Sealed>]
type internal MessageQueue<'T> =

    member HasMessages : bool

    member Push : 'T -> unit

    member Process : ('T -> unit) -> unit

    new : unit -> MessageQueue<'T>