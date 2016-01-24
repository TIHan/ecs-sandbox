namespace ECS.Core

open System

open ECS.Core

[<Sealed>]
type World (maxEntityCount) =
    let eventAggregator = EventAggregator ()
    let entityManager = EntityManager (eventAggregator, maxEntityCount)
    let deps = (entityManager, eventAggregator)

    member this.InitSystem (sys: ISystem) = sys.Init deps

    member this.RunSystem (sys: ISystem) = sys.Update deps
 