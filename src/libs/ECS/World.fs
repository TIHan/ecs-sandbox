namespace ECS.Core

open System

open ECS.Core

[<Sealed>]
type World (maxEntityCount) =
    let eventAggregator = EventAggregator ()
    let entityManager = EntityManager (eventAggregator, maxEntityCount)

    member this.InitSystem (sys: ISystem) = sys.Init (entityManager, eventAggregator)

    member this.RunSystem (sys: ISystem) = sys.Update (entityManager, eventAggregator)
 