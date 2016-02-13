namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T when 'T :> ISystem> (f: unit -> unit) =

    member this.Update = f

[<Sealed>]
type World (maxEntityAmount) =
    let eventManager = EventManager ()
    let entityManager = EntityManager (eventManager, maxEntityAmount)

    member this.AddSystem<'T when 'T :> ISystem> (sys: 'T) =
        match sys.Init (entityManager, eventManager) with
        | SystemUpdate update -> SystemHandle<'T> update
 