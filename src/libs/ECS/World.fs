namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T, 'Data when 'T :> IECSSystem<'Data>> (update: 'Data -> unit) =

    member this.Update = update

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventManager.Create ()
    let entityManager = EntityManager.Create (eventManager, maxEntityAmount)

    member this.AddSystem<'T, 'Data when 'T :> IECSSystem<'Data>> (sys: 'T) =
        sys.HandleEvents
        |> List.iter (fun x -> x.Handle entityManager eventManager)

        SystemHandle<'T, 'Data> (sys.Init entityManager eventManager)
 