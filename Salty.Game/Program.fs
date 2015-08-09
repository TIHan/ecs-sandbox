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
open Salty.Renderer
open Salty.Renderer.Components

#nowarn "9"
#nowarn "51"

open System.Numerics

type PlayerCommand =
    | StartMovingUp = 0
    | StopMovingUp = 1

type Player () =

    member val IsMovingUp = Var.create false

    interface IComponent<Player>

type PhysicsPolygon () =

    member val Data : Vector2 [] Var = Var.create [||]

    member val IsStatic = Var.create false

    member val Density = Var.create 0.f

    member val Restitution = Var.create 0.f

    member val Friction = Var.create 0.f

    member val Mass = Var.create 0.f

    member val Body : FarseerPhysics.Dynamics.Body = null with get, set

    member val PolygonShape : FarseerPhysics.Collision.Shapes.PolygonShape = null with get, set

    member val Fixture : FarseerPhysics.Dynamics.Fixture = null with get, set

    interface IComponent<PhysicsPolygon>

type PhysicsSystem () =

    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))

    interface ISystem with
        
        member __.Init world =
            World.componentAdded<PhysicsPolygon> world
            |> Observable.add (function
                | (entity, physicsPolygon) ->
                    let data = 
                        physicsPolygon.Data.Value

                    physicsPolygon.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)
                    physicsPolygon.Body.BodyType <- if physicsPolygon.IsStatic.Value then FarseerPhysics.Dynamics.BodyType.Static else FarseerPhysics.Dynamics.BodyType.Dynamic
                    physicsPolygon.Body.Restitution <- physicsPolygon.Restitution.Value
                    physicsPolygon.Body.Friction <- physicsPolygon.Friction.Value
                    physicsPolygon.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physicsPolygon.Density.Value)
                    physicsPolygon.Fixture <- physicsPolygon.Body.CreateFixture (physicsPolygon.PolygonShape)
                    physicsPolygon.Fixture.UserData <- entity.Id
                    physicsPolygon.Body.Mass <- physicsPolygon.Mass.Value

                    physicsPolygon.Fixture.OnCollision <-
                        new FarseerPhysics.Dynamics.OnCollisionEventHandler (
                            fun fixture1 fixture2 _ -> 
                                true
                        )
            )

        member __.Update world =
            world.ComponentQuery.ForEach<PhysicsPolygon, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                physicsPolygon.Body.Position <- position.Var |> Var.value
                physicsPolygon.Body.Rotation <- rotation.Var |> Var.value
                physicsPolygon.Body.Awake <- true
            )

            physicsWorld.Step (single world.Time.Interval.Value.TotalSeconds)

            world.ComponentQuery.ForEach<PhysicsPolygon, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                position.Var.Value <- physicsPolygon.Body.Position
                rotation.Var.Value <- physicsPolygon.Body.Rotation

                match world.ComponentQuery.TryGet<Centroid> entity with
                | Some centroid ->
                    centroid.Var.Value <- physicsPolygon.Body.WorldCenter
                | _ -> ()
            )

let boxEntity p (desc: EntityDescription) =
    let data =
        [|
            Vector2 (1.127f, 1.77f)
            Vector2 (0.f, 1.77f)
            Vector2 (0.f, 0.f)
            Vector2 (1.127f, 0.f)
        |]

    let position = Position ()
    position.Var.Value <- p

    let rotation = Rotation ()
    rotation.Var.Value <- 0.f

    let physicsPolygon = PhysicsPolygon ()
    physicsPolygon.Data.Value <- data
    physicsPolygon.Density.Value <- 1.f
    physicsPolygon.Restitution.Value <- 0.f
    physicsPolygon.Friction.Value <- 1.f
    physicsPolygon.Mass.Value <- 1.f
    physicsPolygon.IsStatic.Value <- false

    let render = Render ()
    render.VBO <- Renderer.R.CreateVBO (data)

    desc
    |> Entity.add position
    |> Entity.add rotation
    |> Entity.add physicsPolygon
    |> Entity.add render

let playerBoxEntity position desc =
    boxEntity position desc
    |> Entity.add (Player ())
    |> Entity.add (Input ())

let cameraEntity (desc: EntityDescription) =
    let camera = Camera ()
    camera.Projection <- Matrix4x4.CreateOrthographic (1280.f / 64.f, 720.f / 64.f, 0.1f, 1.f)
    camera.ViewportDimensions <- Vector2 (1280.f, 720.f)
    camera.ViewportDepth <- Vector2 (0.1f, 1.f)

    desc |> Entity.add camera

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
            world.ComponentQuery.ForEach<Player, PhysicsPolygon> (fun (entity, player, physicsPolygon) ->
                if player.IsMovingUp.Value then
                    physicsPolygon.Body.ApplyForce (Vector2.UnitY * 15.f)
            )

///////////////////////////////////////////////////////////////////

let benchmark f =
    let s = Stopwatch.StartNew ()
    f ()
    s.Stop()
    printfn "MS: %A" s.ElapsedMilliseconds

[<EntryPoint>]
let main argv = 

    let inputSystem = InputSystem ()
    let movementSystem = MovementSystem ()
    let physicsSystem = PhysicsSystem ()

    let world = 
        World (65536,
            [
                inputSystem
                movementSystem
                physicsSystem
            ]
        )

    let rendererSystem : ISystem = RendererSystem () :> ISystem
    rendererSystem.Init world

    Entity.create 0
    |> playerBoxEntity Vector2.Zero
    |> Entity.run world


    let comps desc =
        let data =
            [|
                Vector2 (-1000.f, -1.f)
                Vector2 (1000.f, -1.f)
                Vector2 (1000.f, 1.f)
                Vector2 (-1000.f, 1.f)
            |]

        let positionValue = Vector2 (0.f, -2.f)

        let position = Position ()
        position.Var.Value <- positionValue

        let rotation = Rotation ()

        let physicsPolygon = PhysicsPolygon ()
        physicsPolygon.Data.Value <- data
        physicsPolygon.Density.Value <- 1.f
        physicsPolygon.Restitution.Value <- 0.f
        physicsPolygon.Friction.Value <- 1.f
        physicsPolygon.Mass.Value <- 1.f
        physicsPolygon.IsStatic.Value <- true

        let render = Render ()
        render.VBO <- Renderer.R.CreateVBO (data)

        desc
        |> Entity.add position
        |> Entity.add rotation
        |> Entity.add physicsPolygon
        |> Entity.add render

    Entity.create 1
    |> comps
    |> Entity.run world

    Entity.create 2
    |> cameraEntity 
    |> Entity.run world

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