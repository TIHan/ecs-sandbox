namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T when 'T :> IECSSystem> (update: unit -> unit) =

    member this.Update = update

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventManager ()
    let entityManager = EntityManager (eventManager, maxEntityAmount)

    member this.AddSystem<'T when 'T :> IECSSystem> (sys: 'T) =
        sys.HandleEvents
        |> List.iter (fun x -> x.Handle (entityManager, eventManager))

        SystemHandle<'T> (fun () -> sys.Update (entityManager, eventManager))
 