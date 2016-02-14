open ECS
open ECS.World

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type TestComponent =
    {
        Value: int
    }

    interface IECSComponent

type TestComponent2 =
    {
        Value: int
    }

    interface IECSComponent

type TestComponent3 =
    {
        Value: int
    }

    interface IECSComponent

type TestComponent4 =
    {
        Value: int
    }

    interface IECSComponent

type TestComponent5 =
    {
        Value: int
    }

    interface IECSComponent

let benchmark name f =
    printfn "Benchmark: %s" name
    let s = System.Diagnostics.Stopwatch.StartNew ()
    f ()
    s.Stop ()
    printfn "Time: %A ms" s.Elapsed.TotalMilliseconds
    printfn "-------------------"

module Tests =
    open Xunit

    let test =
        let v = 0
        EntityPrototype.empty
        |> EntityPrototype.add<TestComponent> (fun () ->
            {
                Value = v
            }
        )
        |> EntityPrototype.add<TestComponent2> (fun () ->
            {
                Value = v
            }
        )
        |> EntityPrototype.add<TestComponent3> (fun () ->
            {
                Value = v
            }
        )
        |> EntityPrototype.add<TestComponent4> (fun () ->
            {
                Value = v
            }
        )
        |> EntityPrototype.add<TestComponent5> (fun () ->
            {
                Value = v
            }
        )

    let run maxEntityAmount handleEvents f =
        let world = World (maxEntityAmount)

        let entityProcessor = Systems.EntityProcessor ()

        let entityProcessorHandle = world.AddSystem entityProcessor

        let mutable entity = Entity ()

        let sys = Systems.System ("Test", handleEvents, fun entities events ->
            SystemUpdate (fun () -> f entities events entityProcessorHandle)
        )

        (world.AddSystem sys).Update ()

    [<Fact>]
    let ``when max entity amount is 10k, then create and destroy 10k entities with 5 components three times`` () =
        let count = 10000
        run count [] (fun entities events entityProcessorHandle ->
            for i = 1 to 3 do
                for i = 0 to count - 1 do
                    entities.Spawn test

                entityProcessorHandle.Update ()

                entities.ForEach<TestComponent> (fun entity test ->
                    entities.Destroy entity
                )

                entityProcessorHandle.Update ()

                entities.ForEach<TestComponent> (fun _ _ ->
                    failwith "TestComponent was not deleted"
                )

                entities.ForEach<TestComponent2> (fun _ _ ->
                    failwith "TestComponent2 was not deleted"
                )

                entities.ForEach<TestComponent3> (fun _ _ ->
                    failwith "TestComponent3 was not deleted"
                )

                entities.ForEach<TestComponent4> (fun _ _ ->
                    failwith "TestComponent4 was not deleted"
                )

                entities.ForEach<TestComponent5> (fun _ _ ->
                    failwith "TestComponent5 was not deleted"
                )
        )

    [<Fact>]
    let ``when spawning and destroying entities, events happen in the right order`` () =
        let mutable entityCount = 0
        let mutable componentCount = 0
        let count = 10000
        let halfCount = count / 2
        run count 
            [

                HandleEvent<AnyComponentAdded> (fun _ _ ->
                    Assert.Equal (entityCount, 0)
                    componentCount <- componentCount + 1
                )

                HandleEvent<EntitySpawned> (fun _ _ ->
                    Assert.Equal (componentCount, halfCount * 5)
                    entityCount <- entityCount + 1
                )

                HandleEvent<AnyComponentRemoved> (fun _ _ ->
                    Assert.Equal (entityCount, halfCount)
                    componentCount <- componentCount - 1
                )

                HandleEvent<EntityDestroyed> (fun _ _ ->
                    Assert.Equal (componentCount, 0)
                    entityCount <- entityCount - 1
                )

            ] 
            (
                fun entities events entityProcessorHandle ->
                    // Queue to spawn 5000 entities
                    for i = 0 to halfCount - 1 do
                        entities.Spawn test

                    // Process the queued 5000 entities
                    entityProcessorHandle.Update ()

                    // Queue to destroy those 5000 entities
                    entities.ForEach<TestComponent> (fun entity test ->
                        entities.Destroy entity
                    )

                    // Also queue another 5000 entities
                    for i = 0 to halfCount - 1 do
                        entities.Spawn test

                    Assert.Equal (entityCount, halfCount)
                    Assert.Equal (componentCount, halfCount * 5)

                    // Process the destroy and spawn queued entities
                    entityProcessorHandle.Update ()

                    Assert.Equal (entityCount, halfCount)
                    Assert.Equal (componentCount, halfCount * 5)

                    entities.ForEach<TestComponent> (fun entity test ->
                        entities.Destroy entity
                    )

                    entityProcessorHandle.Update ()

                    Assert.Equal (entityCount, 0)
                    Assert.Equal (componentCount, 0)
            )

[<EntryPoint>]
let main argv = 

    Tests.``when max entity amount is 10k, then create and destroy 10k entities with 5 components three times`` ()
    Tests.``when spawning and destroying entities, events happen in the right order`` ()

    printfn "Finished."
    0

