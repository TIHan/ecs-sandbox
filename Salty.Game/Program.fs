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
open Salty.Physics
open Salty.Physics.Components
open Salty.Renderer
open Salty.Renderer.Components

open Salty.Game
open Salty.Game.Core.Components
open Salty.Game.Command

#nowarn "9"
#nowarn "51"

open System.Numerics
open System.Threading

let unProject (source: Vector3, model: Matrix4x4, view: Matrix4x4, projection: Matrix4x4, viewportPosition: Vector2, viewportDimensions: Vector2, viewportDepth: Vector2) =
    let _,m = Matrix4x4.Invert (model * view * projection)
    let x = (((source.X - viewportPosition.X) / (viewportDimensions.X)) * 2.f) - 1.f
    let y = -((((source.Y - viewportPosition.Y) / (viewportDimensions.Y)) * 2.f) - 1.f)
    let z = (source.Z - viewportDepth.X) / (viewportDepth.Y - viewportDepth.X)
    let mutable v = Vector3.Transform(Vector3 (x, y, z), m)
    v

type MovementSystem () =
    let count = ref 10

    interface ISystem with
        
        member __.Init world =
            Physics.collided world
            |> Observable.add (fun ((ent1, phys1), (ent2, phys2)) ->
                match world.ComponentQuery.TryGet<Health> ent1 with
                | None -> ()
                | Some health ->
                    health.Var.Value <- health.Var.Value - 10.f
                    printfn "%A: %A" ent1.Id health.Var.Value
                    if health.Var.Value <= 0.f then
                        world.EntityService.Destroy ent1
            )
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
            world.ComponentQuery.ForEach<Player, Physics> (fun (entity, player, physics) ->
                if player.IsMovingUp.Value then
                    Physics.applyForce (Vector2.UnitY * 15.f) entity world

                if player.IsMovingLeft.Value then
                    Physics.applyForce (Vector2.UnitX * -20.f) entity world

                if player.IsMovingRight.Value then
                    Physics.applyForce (Vector2.UnitX * 20.f) entity world
            )

            match world.ComponentQuery.TryFind<Camera> (fun _ -> true) with
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
                        if distX < 64.f then
                            64.f
                        else
                            distX

                    let distY =
                        if distY < 24.f then
                            24.f
                        else
                            distY

                    let scaleX = (1280.f - 64.f) / distX
                    let c = 720.f / 1280.f
                    let scaleY = c * (1280.f - 64.f) / distY

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

[<EntryPoint>]
let main argv = 

    let world = 
        World (16384,
            [
                CommandSystem ()
                MovementSystem ()
                PhysicsSystem ()
            ]
        )

    let inline runWorld () = world.Run ()

    let world = world :> IWorld

    let rendererSystem : ISystem = RendererSystem () :> ISystem
    rendererSystem.Init world

    EntityBlueprint.create ()
    |> EntityBlueprint.player Vector2.Zero
    |> EntityBlueprint.build world

    EntityBlueprint.create ()
    |> EntityBlueprint.player (Vector2.One * 2.f)
    |> EntityBlueprint.build world

    EntityBlueprint.create ()
    |> EntityBlueprint.staticBox
    |> EntityBlueprint.build world

    EntityBlueprint.create ()
    |> EntityBlueprint.camera
    |> EntityBlueprint.build world

    GameLoop.start
        world
        30.
        (
            fun () ->
                GC.Collect 0
        )
        (
            fun time interval world ->
                world.Time.Interval.Value <- TimeSpan.FromTicks interval
                world.Time.Current.Value <- TimeSpan.FromTicks time
                runWorld ()
        )
        (
            fun delta world ->
                world.Time.Delta.Value <- delta
                rendererSystem.Update world
        )

    0