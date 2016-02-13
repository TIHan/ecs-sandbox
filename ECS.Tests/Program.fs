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

    let createWorld maxEntityAmount f =
        let world = World (maxEntityAmount)

        let entityProcessor = Systems.EntityProcessor ()

        let entityProcessorHandle = world.AddSystem entityProcessor

        let sys = Systems.System ("Test", fun entities events ->
            SystemUpdate (fun () -> f entities events entityProcessorHandle)
        )

        (world.AddSystem sys).Update ()

    [<Fact>]
    let ``create and destroy 10k entities with 5 components, three times`` () =
        createWorld 10000 (fun entities events entityProcessorHandle ->
            for i = 0 to 10000 - 1 do
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

[<EntryPoint>]
let main argv = 
    0

