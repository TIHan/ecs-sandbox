open ECS
open ECS.World
open ECS.Systems

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

let testComponent =
    EntityPrototype.create ()
    |> EntityPrototype.add<TestComponent> (fun () ->
        {
            Value = 1234
        }
    )
    |> EntityPrototype.add<TestComponent2> (fun () ->
        {
            Value = 1234
        }
    )
    |> EntityPrototype.add<TestComponent3> (fun () ->
        {
            Value = 1234
        }
    )
    |> EntityPrototype.add<TestComponent4> (fun () ->
        {
            Value = 1234
        }
    )

let benchmark f =
    let s = System.Diagnostics.Stopwatch.StartNew ()
    f ()
    s.Stop ()
    printfn "Time: %A" s.ElapsedMilliseconds

[<EntryPoint>]
let main argv = 
    let world = World (10000)

    let entityProcessor = EntityProcessor.Create ()

    let entityProcessorHandle = world.AddSystem entityProcessor

    for i = 0 to 10 - 1 do

        for i = 0 to 5 - 1 do
            testComponent
            |> EntityPrototype.spawn world.EntityManager

        benchmark <| fun () ->
            entityProcessorHandle.Update ()

    for i = 0 to 1000 - 1 do
        testComponent
        |> EntityPrototype.spawn world.EntityManager

    printfn "1000 Entities"
    benchmark <| fun () ->
        entityProcessorHandle.Update ()  

//    world.EntityManager.ForEach<TestComponent> (fun entity test ->
//        printfn "%A %A" entity test
//    )

    0

