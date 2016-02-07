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
        sys.Init (entityManager, eventAggregator)
        SystemHandle (fun () -> sys.Update (entityManager, eventAggregator))

    member this.AddSystems (systems: ISystem seq) =
        let systems = systems |> Array.ofSeq

        systems
        |> Seq.iter (fun sys -> sys.Init (entityManager, eventAggregator))

        SystemHandle (fun () ->
            for i = 0 to systems.Length - 1 do
                systems.[i].Update (entityManager, eventAggregator)
        )

    member this.EntityManager = entityManager

    member this.EventAggregator = eventAggregator
 