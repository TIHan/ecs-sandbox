open ECS
open ECS.World

open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<Struct>]
type TestComponent =

    val mutable Value : int

    interface IComponent

type TestComponent2 =
    {
        mutable Value: int
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

[<RequireQualifiedAccess>]
module Entity =

    let test =
        EntityPrototype.create ()
        |> EntityPrototype.add TestComponent
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
        |> EntityPrototype.add<TestComponent5> (fun () ->
            {
                Value = 1234
            }
        )

let benchmark f =
    let s = System.Diagnostics.Stopwatch.StartNew ()
    f ()
    s.Stop ()
    printfn "Time: %A ms" s.Elapsed.TotalMilliseconds

[<EntryPoint>]
let main argv = 
    let world = World (65536)
    let entities = world.EntityManager
    let events = world.EventAggregator

    let entityProcessor = Systems.EntityProcessor ()

    let entityProcessorHandle = world.AddSystem entityProcessor

    for i = 0 to 10000 - 1 do
        entities.Spawn Entity.test

    printfn "10000 Entities"
    benchmark <| fun () ->
        entityProcessorHandle.Update () 

    for i = 0 to 50 - 1 do
        benchmark <| fun () ->
            entities.ForEach<TestComponent, TestComponent2> (fun entity test test2 ->
                test.Value <- 42
                test2 <- { Value = 42 }
            )

    printfn "Memory: %A" <| System.GC.GetTotalMemory (false) / 1024L / 1024L

    0

