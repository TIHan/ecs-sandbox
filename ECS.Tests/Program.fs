open BeyondGames.Ecs
open BeyondGames.Ecs.World

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

//[<Struct>]
type TestComponent =

    val mutable Value11 : int
    val mutable Value12 : int
    val mutable Value13 : int
    val mutable Value14 : int

//    val mutable Value21 : int
//    val mutable Value22 : int
//    val mutable Value23 : int
//    val mutable Value24 : int
//
//    val mutable Value31 : int
//    val mutable Value32 : int
//    val mutable Value33 : int
//    val mutable Value34 : int
//
//    val mutable Value41 : int
//    val mutable Value42 : int
//    val mutable Value43 : int
//    val mutable Value44 : int

    interface IEntityComponent

    new (
        v11, v12, v13, v14) =
//        v21, v22, v23, v24,
//        v31, v32, v33, v34,
//        v41, v42, v43, v44) =
                                {
                                    Value11 = v11
                                    Value12 = v12
                                    Value13 = v13
                                    Value14 = v14

//                                    Value21 = v21
//                                    Value22 = v22
//                                    Value23 = v23
//                                    Value24 = v24
//
//                                    Value31 = v31
//                                    Value32 = v32
//                                    Value33 = v33
//                                    Value34 = v34
//
//                                    Value41 = v41
//                                    Value42 = v42
//                                    Value43 = v43
//                                    Value44 = v44
                                }

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
        EntityPrototype.empty
        |> EntityPrototype.addComponent<TestComponent> (fun () ->
            TestComponent (
                0, 0, 0, 0
//                1, 1, 1, 1,
//                2, 2, 2, 2,
//                3, 3, 3, 3
            )
        )

    let run maxEntityAmount handleEvents f =
        let world = World (maxEntityAmount)

        let entityProcessor = EntitySystems.EntityProcessor ()

        let entityProcessorHandle = world.AddSystem entityProcessor

        let sys = EntitySystems.EntitySystem ("Test", handleEvents, fun entities events () ->
            f entities events entityProcessorHandle
        )

        (world.AddSystem sys).Update ()

    let ``when max entity amount is 10k, then creating and destroying 10k entities with 5 components three times will not fail`` () =
        let count = 10000
        run count [] (fun entities events entityProcessorHandle ->
            for i = 0 to count - 1 do
                entities.Spawn test

            entityProcessorHandle.Update ()

            let aspect = entities.GetAspect<TestComponent, TestComponent> ()
            let del : ForEachDelegate<TestComponent, TestComponent> = 
                (fun _ comp _ ->
                    comp.Value11 <- 0
                    comp.Value12 <- 0
                    comp.Value13 <- 0
                    comp.Value14 <- 0

//                    comp.Value21 <- 1
//                    comp.Value22 <- 1
//                    comp.Value23 <- 1
//                    comp.Value24 <- 1
//
//                    comp.Value31 <- 2
//                    comp.Value32 <- 2
//                    comp.Value33 <- 2
//                    comp.Value34 <- 2
//
//                    comp.Value41 <- 3
//                    comp.Value42 <- 3
//                    comp.Value43 <- 3
//                    comp.Value44 <- 3
                )

            let yopac (x: ForEachDelegate<TestComponent>) =
                ()

            let bench = fun () -> 
                for i = 0 to 0 do
                    aspect.ForEach del

            let invoke = fun () -> benchmark "Cache Locality" bench

            for i = 1 to 50000 do
                invoke ()
                    

        )

type TestMe =

    val mutable X : int

    new (x) = { X = x }

[<Struct>]
type TestStruct =

    val X : int

    val TestMe : TestMe

    private new (x) = { X = x; TestMe = TestMe (x) }


[<EntryPoint>]
let main argv = 

    let mutable test = Unchecked.defaultof<TestStruct>

    //test.TestMe.X <- 1

    printfn "Starting tests."

    Tests.``when max entity amount is 10k, then creating and destroying 10k entities with 5 components three times will not fail`` ()

    printfn "Finished."
    0

