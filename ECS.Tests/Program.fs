open ECS
open ECS.World

open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type TestComponent =
    {
        mutable Value: int
    }

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

    let test v =
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

let benchmark f =
    let s = System.Diagnostics.Stopwatch.StartNew ()
    f ()
    s.Stop ()
    printfn "Time: %A ms" s.Elapsed.TotalMilliseconds

[<EntryPoint>]
let main argv = 
    let world = World (65536)

    let entityProcessor = Systems.EntityProcessor ()

    let entityProcessorHandle = world.AddSystem entityProcessor

    let sys = Systems.System ("Test", fun entities events ->
        SystemUpdate (fun () ->
            for i = 0 to 3 do
                benchmark <| fun () ->
                    for i = 0 to 10000 - 1 do
                        entities.Spawn (Entity.test i)
                    entityProcessorHandle.Update ()
                    entities.ForEach<TestComponent> (fun entity test ->
                        entities.Destroy entity
                    )
                    entityProcessorHandle.Update ()

            printfn "Memory: %A" <| System.GC.GetTotalMemory (false) / 1024L / 1024L
        )
    )

    (world.AddSystem sys).Update ()

    0

