open ECS.Core
open Salty.Core
open Salty.Core.Components
open Salty.Core.Input
open Salty.Core.Physics
open Salty.Core.Physics.Components
open Salty.Core.Renderer
open Salty.Core.Renderer.Components

open Salty.Game
open Salty.Game.Core.Components
open Salty.Game.Command

open System
open System.IO
open System.Diagnostics
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open System.Reactive.Subjects
open System.Reactive.Linq

#nowarn "9"
#nowarn "51"

open System.Numerics
open System.Threading

open System.Reactive.Linq
[<AutoOpen>]
module DSL =

    let DoNothing : World<_, unit> = fun _ -> ()

    let inline worldReturn (a: 'a) : World<_, 'a> = fun _ -> a

    let inline (>>=) (w: World<_, 'a>) (f: 'a -> World<_, 'b>) : World<_, 'b> =
        fun world -> (f (w world)) world

    let inline (>>.) (w1: World<_, 'a>) (w2: World<_, 'b>) =
        fun world ->
            w1 world |> ignore
            w2 world

    let inline skip (x: World<_, _>) : World<_, unit> =
        fun world -> x world |> ignore

    let inline when' (w: World<_, IObservable<'a>>) (f: 'a -> World<_, unit>) : World<_, IDisposable> =
        fun world ->
            (w world) 
            |> Observable.subscribe (fun a ->
                (f a) world
            )

    let inline (<~) (obs: IObservable<'a>) (f: 'a -> World<_, unit>) : World<_, unit> =
        fun world -> obs |> Observable.add (fun x -> (f x) world)

    let inline (==>) (obs: IObservable<'a>) (v: Val<'a>) : World<_, unit> =
        fun world -> v.Assign obs
            
    let inline update (v: Var<'a>) (f: 'a -> 'a) : World<_, unit> =
        fun world -> v.Value <- f v.Value 

let unProject (source: Vector3, model: Matrix4x4, view: Matrix4x4, projection: Matrix4x4, viewportPosition: Vector2, viewportDimensions: Vector2, viewportDepth: Vector2) =
    let _,m = Matrix4x4.Invert (model * view * projection)
    let x = (((source.X - viewportPosition.X) / (viewportDimensions.X)) * 2.f) - 1.f
    let y = -((((source.Y - viewportPosition.Y) / (viewportDimensions.Y)) * 2.f) - 1.f)
    let z = (source.Z - viewportDepth.X) / (viewportDepth.Y - viewportDepth.X)
    let mutable v = Vector3.Transform(Vector3 (x, y, z), m)
    v

type GameplaySystem () =
    let count = ref 10

    interface ISystem<Salty> with
        
        member __.Init world =

            [
            ]
            |> List.iter (fun f -> f world |> ignore)

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
            world.ComponentQuery.ForEach<Player, Physics> (fun entity player physics ->
                player.Commands
                |> Seq.iter (function
                    | Shoot ->
                        let blueprint =
                            EntityBlueprint.create ()
                            |> EntityBlueprint.test physics.Position.Value
                        for i = 0 to 50000 - 1 do
                            blueprint
                            |> EntityBlueprint.spawn (!count) world
                            count := !count + 1
                )
                player.Commands.Clear ()

                if player.IsMovingUp.Value then
                    Physics.applyImpulse (Vector2.UnitY * 2.f) physics world

                if player.IsMovingLeft.Value then
                    Physics.applyImpulse (Vector2.UnitX * -2.f) physics world

                if player.IsMovingRight.Value then
                    Physics.applyImpulse (Vector2.UnitX * 2.f) physics world
            )

            match world.ComponentQuery.TryFind<Camera> (fun _ _ -> true) with
            | Some (_, camera) ->
                let players = world.ComponentQuery.Get<Player, Position> ()
                let positions =
                    players
                    |> Array.map (fun (_,_,position) -> position.Var.Value)

                let sum =
                    positions
                    |> Array.sum

                let center = Vector2.Divide (sum, single players.Length)
                camera.Position.Value <- center

                if positions.Length > 0 then
                    let minX = (positions |> Array.minBy (fun v -> v.X)).X 
                    let maxX = (positions |> Array.maxBy (fun v -> v.X)).X
                    let minY = (positions |> Array.minBy (fun v -> v.Y)).Y 
                    let maxY = (positions |> Array.maxBy (fun v -> v.Y)).Y

                    let distX = maxX - minX
                    let distY = maxY - minY

                    let distX =
                        if distX < 32.f then
                            32.f
                        else
                            distX

                    let distY =
                        if distY < 12.f then
                            12.f
                        else
                            distY

                    let scaleX = (1280.f - 128.f) / distX
                    let c = 720.f / 1280.f
                    let scaleY = c * (1280.f - 128.f) / distY

                    let scale =
                        if scaleX > scaleY then
                            scaleY
                        else
                            scaleX

                    camera.Projection <- Matrix4x4.CreateOrthographic (1280.f / scale, 720.f / scale, 0.1f, 1.f)
            | _ -> ()

///////////////////////////////////////////////////////////////////

let benchmark f =
    let s = Stopwatch.StartNew ()
    f ()
    s.Stop()
    printfn "MS: %A" s.ElapsedMilliseconds

open Foom.Shared.ResourceManager
open Foom.Shared.Wad

[<EntryPoint>]
let main argv = 

    let currentTimeVar = Var.create TimeSpan.Zero
    let deltaTimeVar = Var.create 0.f
    let intervalVar = Var.create TimeSpan.Zero
    let world = 
        ECSWorld (
            {
                DeltaTime = Val.createWithObservable 0.f deltaTimeVar
                CurrentTime = Val.createWithObservable TimeSpan.Zero currentTimeVar
                Interval = Val.createWithObservable TimeSpan.Zero intervalVar
            }, 
            65536,
            [
                InputSystem ()
                CommandSystem ()
                GameplaySystem ()
                PhysicsSystem ()
                //SerializationSystem ()
            ]
        )

    let inline runWorld () = world.Run ()

    let world = world :> IWorld<Salty>

    let rendererSystem = RendererSystem () :> ISystem<Salty>
    rendererSystem.Init world

    EntityBlueprint.create ()
    |> EntityBlueprint.player Vector2.Zero
    |> EntityBlueprint.spawn 0 world

//    EntityBlueprint.create ()
//    |> EntityBlueprint.player (Vector2.One * 2.f)
//    |> EntityBlueprint.spawn 1 world

    EntityBlueprint.create ()
    |> EntityBlueprint.staticBox
    |> EntityBlueprint.spawn 2 world

    EntityBlueprint.create ()
    |> EntityBlueprint.camera
    |> EntityBlueprint.spawn 3 world

    let stopwatch = Stopwatch.StartNew ()

    GameLoop.start
        world
        30.
        (
            fun () ->
                GC.Collect 0
        )
        (
            fun time interval world ->
                stopwatch.Restart ()

                currentTimeVar.Value <- TimeSpan.FromTicks time
                intervalVar.Value <- TimeSpan.FromTicks interval
                runWorld ()

                stopwatch.Stop ()

                printfn "MS: %A" stopwatch.ElapsedMilliseconds
        )
        (
            fun delta world ->
                deltaTimeVar.Value <- delta
                rendererSystem.Update world
        )

    0