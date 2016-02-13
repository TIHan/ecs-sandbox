namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T when 'T :> ISystem> (f: unit -> unit) =

    member this.Update = f

[<Sealed>]
type World (maxEntityAmount) =
    let eventAggregator = EventAggregator ()
    let entityManager = EntityManager (eventAggregator, maxEntityAmount)

    member this.AddSystem<'T when 'T :> ISystem> (sys: 'T) =
        match sys.Init (entityManager, eventAggregator) with
        | SystemUpdate update -> SystemHandle<'T> update
 