open System
open System.IO
open System.Diagnostics
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open System.Reactive.Subjects
open System.Reactive.Linq

open ECS.Core

open Salty.Core.Components
open Salty.Input
open Salty.Input.Components
open Salty.Physics
open Salty.Physics.Components
open Salty.Renderer
open Salty.Renderer.Components

open Salty.Game

#nowarn "9"
#nowarn "51"

open System.Numerics

type PlayerCommand =
    | StartMovingUp = 0
    | StopMovingUp = 1

type Player () =

    member val IsMovingUp = Var.create false

    interface IComponent<Player>

type MovementSystem () =
    let count = ref 10

    interface ISystem with
        
        member __.Init _ =
            ()
//            world.EventAggregator.GetEvent<InputEvent> ()
//            |> Observable.add (fun (InputEvents (events)) ->
//
//                world.EntityQuery.ForEachActiveComponent<Player, PhysicsPolygon> (fun (entity, _, physicsPolygon) ->
//
////                    if Input.isKeyPressed 'a' then
////                        physicsPolygon.Body.ApplyForce (Vector2.UnitX * -20.f)
////
////                    if Input.isKeyPressed 'd' then
////                        physicsPolygon.Body.ApplyForce (Vector2.UnitX * 20.f)
////
////                    if Input.isKeyPressed 'w' then
////                        physicsPolygon.Body.ApplyForce (Vector2.UnitY * 15.f)
//
//                    events
//                    |> List.iter (function
////                        | JoystickButtonPressed 2 -> 
////                            physicsPolygon.Body.ApplyForce (Vector2.UnitX * -20.f)
////                        | JoystickButtonPressed 3 -> 
////                            physicsPolygon.Body.ApplyForce (Vector2.UnitX * 20.f)
////                        | JoystickButtonPressed 10 -> 
////                            physicsPolygon.Body.ApplyForce (Vector2.UnitY * 50.0f)
//
//                        | MouseButtonToggled (MouseButtonType.Left, true) ->
//
//                            let cameras = world.EntityQuery.GetComponents<Camera> ()
//
//                            match cameras with
//                            | [||] -> ()
//                            | _ ->
//            
//                            let (_,camera) = cameras.[0]
//
//                            let mouse = Input.getMousePosition()
//                            let mouseV = Vector3 (single mouse.X, single mouse.Y, 0.f)
//
//                            let v = unProject (mouseV, Matrix4x4.Identity, camera.View, camera.Projection, camera.ViewportPosition, camera.ViewportDimensions, camera.ViewportDepth)
//                            let v = Vector2 (v.X, v.Y)
//
//                            let n = !count
//                            count := n + 1
//
//                            world.EntityFactory.CreateActive n <| boxEntity v 
//                                                   
//                        | _ -> ()
//                    )
//                )



//            )

        member __.Update world =
            world.ComponentQuery.ForEach<Player, Physics> (fun (entity, player, physicsPolygon) ->
                ()
                //if player.IsMovingUp.Value then
                    //physicsPolygon.Body.ApplyForce (Vector2.UnitY * 15.f)
            )

///////////////////////////////////////////////////////////////////

let benchmark f =
    let s = Stopwatch.StartNew ()
    f ()
    s.Stop()
    printfn "MS: %A" s.ElapsedMilliseconds

[<EntryPoint>]
let main argv = 

    let world = 
        World (65536,
            [
                InputSystem ()
                MovementSystem ()
                PhysicsSystem ()
            ]
        )

    let rendererSystem : ISystem = RendererSystem () :> ISystem
    rendererSystem.Init world

    EntityBlueprint.create ()
    |> EntityBlueprint.player Vector2.Zero
    |> EntityBlueprint.build world

    EntityBlueprint.create ()
    |> EntityBlueprint.staticBox
    |> EntityBlueprint.build world

    EntityBlueprint.create ()
    |> EntityBlueprint.camera
    |> EntityBlueprint.build world

    let stopwatch = Stopwatch ()

    GameLoop.start
        world
        25.
        (
            fun () ->
                GC.Collect 0
        )
        (
            fun time interval world ->
                stopwatch.Restart ()


                (world :> IWorld).Time.Current.Value <- TimeSpan.FromTicks time
                (world :> IWorld).Time.Interval.Value <- TimeSpan.FromTicks interval
                world.Run ()

                stopwatch.Stop ()
        )
        (
            fun delta world ->
                (world :> IWorld).Time.Delta.Value <- delta
                rendererSystem.Update world
                //Console.Clear ()

                //printfn "FPS: %.2f" (1000.f / single stopwatch.ElapsedMilliseconds)
                printfn "Update MS: %A" stopwatch.ElapsedMilliseconds
        )

    0