namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T when 'T :> ISystem> (update: unit -> unit) =

    member this.Update = update

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventManager ()
    let entityManager = EntityManager (eventManager, maxEntityAmount)

    member this.AddSystem<'T when 'T :> ISystem> (sys: 'T) =

        sys.HandleEvents
        |> List.iter (fun x -> x.Handle (entityManager, eventManager))

        match sys.Init (entityManager, eventManager) with
        | SystemUpdate update -> SystemHandle<'T> update
 