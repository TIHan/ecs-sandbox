namespace BeyondGames.Ecs.World

open System
open System.Collections.Generic
open System.Threading
open BeyondGames.Ecs

[<Sealed>]
type SystemHandle<'UpdateData> (update: 'UpdateData -> unit, subs: IDisposable [], disposableOpt: IDisposable option) =
    let mutable update = update
    let mutable isDisposed = ref false

    member this.Update data =
        if not !isDisposed then
            update data

    member this.Dispose () =
        let isDisposed = Interlocked.Exchange(&isDisposed, ref true)
        if not !isDisposed then 
            subs |> Array.iter(fun s -> s.Dispose())
            disposableOpt |> Option.iter (fun x -> x.Dispose ())
            update <- (fun _ -> ())

    interface IDisposable with
        member this.Dispose() =
            this.Dispose()

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventManager.Create ()
    let entityManager = EntityManager.Create (eventManager, maxEntityAmount)

    member this.AddSystem<'T, 'UpdateData when 'T :> IEntitySystem<'UpdateData>> (sys: 'T) =
        let subs =
            sys.Events
            |> List.map (fun x -> x.Handle entityManager eventManager)
            |> List.toArray

        let disposableOpt : IDisposable option =
            match (sys :> obj) with
            | :? IEntitySystemShutdown as sys -> Some { new IDisposable with member __.Dispose () = sys.Shutdown () }
            | _ -> None

        new SystemHandle<'UpdateData> (sys.Initialize entityManager eventManager, subs, disposableOpt)
