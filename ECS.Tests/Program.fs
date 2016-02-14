open ECS
open ECS.World

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type TestComponent =
    {
        Value: int
    }

    interface IComponent

type TestComponent2 =
    {
        Value: int
    }

    interface IComponent

type TestComponent3 =
    {
        Value: int
    }

    interface IComponent

type TestComponent4 =
    {
        Value: int
    }

    interface IComponent

type TestComponent5 =
    {
        Value: int
    }

    interface IComponent

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
        EntityPrototype.create ()
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

    let createWorld maxEntityAmount handleEvents f =
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
        createWorld count [] (fun entities events entityProcessorHandle ->
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
        createWorld count 
            [

                HandleEvent<AnyComponentAdded> (fun _ _ ->
                    Assert.True ((entityCount = 0))
                    componentCount <- componentCount + 1
                )

                HandleEvent<EntitySpawned> (fun _ _ ->
                    Assert.True ((componentCount = count * 5))
                    entityCount <- entityCount + 1
                )

                HandleEvent<AnyComponentRemoved> (fun _ _ ->
                    Assert.True ((entityCount = count))
                    componentCount <- componentCount - 1
                )

                HandleEvent<EntityDestroyed> (fun _ _ ->
                    Assert.True ((componentCount = 0))
                    entityCount <- entityCount - 1
                )

            ] 
            (fun entities events entityProcessorHandle ->
                for i = 0 to count - 1 do
                    entities.Spawn test

                entityProcessorHandle.Update ()

                entities.ForEach<TestComponent> (fun entity test ->
                    entities.Destroy entity
                )

                Assert.True ((entityCount = count))
                Assert.True ((componentCount = count * 5))

                entityProcessorHandle.Update ()

                Assert.True ((entityCount = 0))
                Assert.True ((componentCount = 0))
            )

[<EntryPoint>]
let main argv = 

    Tests.``when max entity amount is 10k, then create and destroy 10k entities with 5 components three times`` ()
    Tests.``when spawning and destroying entities, events happen in the right order`` ()
    
    0

