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

    let test1Only =
        EntityPrototype.empty
        |> EntityPrototype.addComponent<TestComponent> (fun () ->
            {
                Value = 0
            }
        )

    let test =
        test1Only
        |> EntityPrototype.addComponent<TestComponent2> (fun () ->
            {
                Value = 0
            }
        )
        |> EntityPrototype.addComponent<TestComponent3> (fun () ->
            {
                Value = 0
            }
        )
        |> EntityPrototype.addComponent<TestComponent4> (fun () ->
            {
                Value = 0
            }
        )
        |> EntityPrototype.addComponent<TestComponent5> (fun () ->
            {
                Value = 0
            }
        )

    let run maxEntityAmount handleEvents f =
        let world = World (maxEntityAmount)

        let entityProcessor = Systems.EntityProcessor ()

        let entityProcessorHandle = world.AddSystem entityProcessor

        let sys = Systems.System ("Test", handleEvents, fun entities events () ->
            f entities events entityProcessorHandle
        )

        (world.AddSystem sys).Update ()

    [<Fact>]
    let ``when max entity amount is 10k, then creating and destroying 10k entities with 5 components three times will not fail`` () =
        let count = 10000
        run count [] (fun entities events entityProcessorHandle ->
            for i = 1 to 3 do
                for i = 0 to count - 1 do
                    entities.Spawn test

                entityProcessorHandle.Update ()

                let mutable entityCount = 0

                entities.ForEach<TestComponent> (fun entity test ->
                    entityCount <- entityCount + 1
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

                Assert.Equal (entityCount, count)
        )

    [<Fact>]
    let ``when spawning and destroying entities, then events happen in the right order`` () =
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

    [<Fact>]
    let ``when an added component event is handled, then component exists`` () =
        let mutable componentExists = false
        run 1
            [
                HandleEvent<ComponentAdded<TestComponent>> (fun entities event ->
                    match entities.TryGet<TestComponent> event.Entity with
                    | Some _ -> componentExists <- true
                    | _ -> ()
                )
            ]
            (
                fun entities events entityProcessorHandle -> 
                    entities.Spawn test1Only

                    entityProcessorHandle.Update ()

                    Assert.True (componentExists)

            )

    [<Fact>]
    let ``when a removed component event is handled, then component doesn't exist`` () =
        let mutable componentExists = true
        run 1
            [
                HandleEvent<ComponentAdded<TestComponent>> (fun entities event ->
                    match entities.TryGet<TestComponent> event.Entity with
                    | Some _ -> componentExists <- true
                    | _ -> ()

                    entities.Destroy event.Entity
                )

                HandleEvent<ComponentRemoved<TestComponent>> (fun entities event ->
                    match entities.TryGet<TestComponent> event.Entity with
                    | None -> componentExists <- false
                    | _ -> ()
                )
            ]
            (
                fun entities events entityProcessorHandle ->
                    entities.Spawn test1Only

                    entityProcessorHandle.Update ()

                    Assert.False (componentExists)

            )
    
    [<Fact>]
    let ``when an added component event is handled, then destroying the entity and spawning a different one will not fail`` () =
        run 1
            [
                HandleEvent<ComponentAdded<TestComponent5>> (fun entities event ->
                    entities.Destroy event.Entity
                    entities.Spawn test1Only
                )
            ]
            (
                fun entities events entityProcessorHandle ->
                    entities.Spawn test

                    entityProcessorHandle.Update ()

                    entities.ForEach<TestComponent5> (fun _ _ ->
                        failwith "TestComponent not deleted"
                    )

                    let mutable isNewEntitySpawned = false
                    entities.ForEach<TestComponent> (fun _ _ ->
                        isNewEntitySpawned <- true
                    )

                    Assert.True (isNewEntitySpawned)

            )

    [<Fact>]
    let ``when a system with a float32 delta time value is required, the value is valid`` () =
        let world = World (1)

        let expectedDeltaTime = 0.25f

        let sys = Systems.System<float32> ("Test DeltaTime", [], fun entities events deltaTime ->
            Assert.Equal (deltaTime, expectedDeltaTime)
        )

        (world.AddSystem sys).Update (0.25f)

[<EntryPoint>]
let main argv = 

    printfn "Starting tests."

    Tests.``when max entity amount is 10k, then creating and destroying 10k entities with 5 components three times will not fail`` ()
    Tests.``when spawning and destroying entities, then events happen in the right order`` ()
    Tests.``when an added component event is handled, then component exists`` ()
    Tests.``when a removed component event is handled, then component doesn't exist`` ()
    Tests.``when an added component event is handled, then destroying the entity and spawning a different one will not fail`` ()
    Tests.``when a system with a float32 delta time value is required, the value is valid`` () 

    printfn "Finished."
    0

