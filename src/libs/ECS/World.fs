namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle (f: unit -> unit) =

    member this.Update = f

[<Sealed>]
type World (maxEntityAmount) =
    let eventAggregator = EventAggregator ()
    let entityManager = EntityManager (eventAggregator, maxEntityAmount)

    member this.AddSystem (sys: ISystem) =
        match sys.Init (entityManager, eventAggregator) with
        | SystemUpdate update -> SystemHandle update

    member this.AddSystems (systems: ISystem seq) =
        let systems = systems |> Array.ofSeq

        let updates =
            systems
            |> Array.map (fun sys -> sys.Init (entityManager, eventAggregator))
            |> Array.map (function | SystemUpdate update -> update)

        SystemHandle (fun () ->
            for i = 0 to updates.Length - 1 do
                updates.[i] ()
        )

    member this.EntityManager = entityManager

    member this.EventAggregator = eventAggregator
 