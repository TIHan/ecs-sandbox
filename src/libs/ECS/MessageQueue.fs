namespace ECS.Core

open System.Collections.Concurrent

[<Sealed>]
type MessageQueue<'T> () =
    let queue = ConcurrentQueue<'T> ()

    member __.HasMessages = queue.Count > 0

    member __.Push msg = queue.Enqueue msg

    member this.Process f =
        let mutable msg : 'T = Unchecked.defaultof<'T>
        while queue.TryDequeue (&msg) do
            f msg
