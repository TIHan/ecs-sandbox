namespace ECS.Core

open System.Collections.Concurrent

[<Sealed>]
type MessageQueue<'T> () =
    let queue = ConcurrentQueue<'T> ()

    member __.HasMessages = queue.Count > 0

    member __.Push msg = queue.Enqueue msg

    member __.TryPop () =
        match queue.TryDequeue () with
        | true, msg -> Some msg
        | _ -> None

    member this.Process f =
        let rec p () =
            match this.TryPop () with
            | Some msg -> 
                f msg
                p ()
            | _ -> ()
        p ()
