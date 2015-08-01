﻿open System
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

#nowarn "9"
#nowarn "51"

open System.Numerics

type Health =
    {
        Value: int ref
    }

type Armor =
    {
        Value: int ref
    }

type Weapon =
    {
        Damage: int
    }

type Player = Player of unit with

    interface IComponent

type Camera =
    {
        mutable Projection: Matrix4x4
        mutable View: Matrix4x4
        mutable ViewportPosition: Vector2
        mutable ViewportDimensions: Vector2
        mutable ViewportDepth: Vector2
        Position: Val<Vector2>
        PreviousPosition: Val<Vector2>
    }

    interface IComponent

type Render =
    {
        R: byte
        G: byte
        B: byte
        VBO: Renderer.VBO
        Position: Val<Vector2>
        PreviousPosition: Val<Vector2>
        Rotation: Val<single>
        PreviousRotation: Val<single>
    }

    interface IComponent

///////////////////////////////////////////////////////////////////

type PhysicsPolygon =
    {
        Data: Vector2 []
        IsStatic: bool
        Density: float32
        Restitution: float32
        Friction: float32
        Mass: float32
        mutable Body: FarseerPhysics.Dynamics.Body
        mutable PolygonShape: FarseerPhysics.Collision.Shapes.PolygonShape
        mutable Fixture: FarseerPhysics.Dynamics.Fixture
    }

    interface IComponent

type PhysicsSystem () =
    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))
    interface ISystem with
        
        member __.Init world =
            world.EventAggregator.GetEvent<ComponentEvent<PhysicsPolygon>> ()
            |> Observable.add (function
                | Added (entity, physicsPolygon) ->
                    let data = 
                        physicsPolygon.Data

                    physicsPolygon.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)
                    physicsPolygon.Body.BodyType <- if physicsPolygon.IsStatic then FarseerPhysics.Dynamics.BodyType.Static else FarseerPhysics.Dynamics.BodyType.Dynamic
                    physicsPolygon.Body.Restitution <- physicsPolygon.Restitution
                    physicsPolygon.Body.Friction <- physicsPolygon.Friction
                    physicsPolygon.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physicsPolygon.Density)
                    physicsPolygon.Fixture <- physicsPolygon.Body.CreateFixture (physicsPolygon.PolygonShape)
                    physicsPolygon.Fixture.UserData <- entity.Id
                    physicsPolygon.Body.Mass <- physicsPolygon.Mass

                    physicsPolygon.Fixture.OnCollision <-
                        new FarseerPhysics.Dynamics.OnCollisionEventHandler (
                            fun fixture1 fixture2 _ -> 
                                true
                        )
                | _ -> ()
            )

        member __.Update world =
            world.EntityQuery.ForEachActiveComponent<PhysicsPolygon, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                physicsPolygon.Body.Position <- position.Var |> Var.value
                physicsPolygon.Body.Rotation <- rotation.Var |> Var.value
                physicsPolygon.Body.Awake <- true
            )

            physicsWorld.Step (single world.Interval.TotalSeconds)

            world.EntityQuery.ForEachActiveComponent<PhysicsPolygon, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                position.Var.Value <- physicsPolygon.Body.Position
                rotation.Var.Value <- physicsPolygon.Body.Rotation

                match world.EntityQuery.TryGetComponent<Centroid> entity with
                | Some centroid ->
                    centroid.Var.Value <- physicsPolygon.Body.WorldCenter
                | _ -> ()
            )

///////////////////////////////////////////////////////////////////

let unProject (source: Vector3, model: Matrix4x4, view: Matrix4x4, projection: Matrix4x4, viewportPosition: Vector2, viewportDimensions: Vector2, viewportDepth: Vector2) =
    let _,m = Matrix4x4.Invert (model * view * projection)
    let x = (((source.X - viewportPosition.X) / (viewportDimensions.X)) * 2.f) - 1.f
    let y = -((((source.Y - viewportPosition.Y) / (viewportDimensions.Y)) * 2.f) - 1.f)
    let z = (source.Z - viewportDepth.X) / (viewportDepth.Y - viewportDepth.X)
    let mutable v = Vector3.Transform(Vector3 (x, y, z), m)
    v

type RendererSystem () =
    let mutable context = Renderer.RendererContext ()
    let mutable vao = Renderer.VAO (0)
    let mutable defaultShader = 0

    interface ISystem with

        member __.Init world =
            Renderer.R.InitSDL ()
            let window = Renderer.R.CreateWindow ()
            context <- Renderer.R.Init (window)
            vao <- Renderer.R.CreateVao ()
            defaultShader <- Renderer.R.LoadShaders ("SimpleVertexShader.vertexshader", "SimpleFragmentShader.fragmentshader")

            world.EventAggregator.GetEvent<ComponentEvent<Render>> ()
            |> Observable.add (function
                | Added (entity, comp) ->
                    comp.PreviousPosition.Assign (world.Time.DistinctUntilChanged().Zip(comp.Position, fun _ x -> x))
                    comp.PreviousRotation.Assign (world.Time.DistinctUntilChanged().Zip(comp.Rotation, fun _ x -> x))
                | _ -> ()
            )

            world.EventAggregator.GetEvent<ComponentEvent<Camera>> ()
            |> Observable.add (function
                | Added (entity, comp) ->
                    comp.PreviousPosition.Assign (world.Time.DistinctUntilChanged().Zip(comp.Position, fun _ x -> x))
                | _ -> ()
            )

            world.EventAggregator.GetEvent<ComponentEvent<Position>> ()
            |> Observable.add (function
                | Added (entity, position) ->
                    match world.EntityQuery.TryGetComponent<Render> entity with
                    | Some render ->
                        render.Position.Assign position.Var
                    | _ -> ()
                | _ -> ()
            )

            world.EventAggregator.GetEvent<ComponentEvent<Centroid>> ()
            |> Observable.add (function
                | Added (entity, centroid) ->
                    match world.EntityQuery.TryGetComponent<Camera> entity with
                    | Some camera ->
                        camera.Position.Assign centroid.Var
                    | _ -> ()
                | _ -> ()
            )

            world.EventAggregator.GetEvent<ComponentEvent<Rotation>> ()
            |> Observable.add (function
                | Added (entity, rotation) ->
                    match world.EntityQuery.TryGetComponent<Render> entity with
                    | Some render ->
                        render.Rotation.Assign rotation.Var
                    | _ -> ()
                | _ -> ()
            )

        member __.Update world =
            Renderer.R.Clear ()

            let cameras = world.EntityQuery.GetComponents<Camera> ()

            match cameras with
            | [||] -> ()
            | _ ->
            
            let (_,camera) = cameras.[0]
            let projection = camera.Projection
            let view = ref camera.View

            world.EntityQuery.ForEachActiveComponent<Player, Camera> (fun (_, _, camera) ->
                let value = Vector2.Lerp (camera.PreviousPosition.Value, camera.Position.Value, world.Delta)
                view := Matrix4x4.CreateTranslation (Vector3 (value, 0.f) * -1.f)
            )

            camera.View <- !view

            Renderer.R.UseProgram defaultShader
            Renderer.R.SetProjection defaultShader projection
            Renderer.R.SetView defaultShader !view

            world.EntityQuery.ForEachActiveComponent<Render, Position> (fun (entity, render, position) ->
                let position = render.Position.Value
                let rotation = render.Rotation.Value

                let positionValue = Vector2.Lerp (render.PreviousPosition.Value, render.Position.Value, world.Delta)
                let rotationValue = Vector2.Lerp(Vector2 (render.PreviousRotation.Value, 0.f), Vector2 (render.Rotation.Value, 0.f), world.Delta).X

                let rotationMatrix = Matrix4x4.CreateRotationZ (rotationValue)
                let model = rotationMatrix * Matrix4x4.CreateTranslation (Vector3 (positionValue, 0.f))

                Renderer.R.SetModel defaultShader model

                Renderer.R.DrawLineLoop defaultShader render.VBO
            )

            Renderer.R.Draw (context)

type MovementSystem () =
    let count = ref 10

    interface ISystem with
        
        member __.Init world =
            world.EventAggregator.GetEvent<InputEvents> ()
            |> Observable.add (fun (InputEvents (events)) ->

                world.EntityQuery.ForEachActiveComponent<Player, PhysicsPolygon> (fun (entity, _, physicsPolygon) ->

                    if Input.isKeyPressed 'a' then
                        physicsPolygon.Body.ApplyForce (Vector2.UnitX * -20.f)

                    if Input.isKeyPressed 'd' then
                        physicsPolygon.Body.ApplyForce (Vector2.UnitX * 20.f)

                    if Input.isKeyPressed 'w' then
                        physicsPolygon.Body.ApplyForce (Vector2.UnitY * 15.f)

                    events
                    |> List.iter (function
//                        | JoystickButtonPressed 2 -> 
//                            physicsPolygon.Body.ApplyForce (Vector2.UnitX * -20.f)
//                        | JoystickButtonPressed 3 -> 
//                            physicsPolygon.Body.ApplyForce (Vector2.UnitX * 20.f)
//                        | JoystickButtonPressed 10 -> 
//                            physicsPolygon.Body.ApplyForce (Vector2.UnitY * 50.0f)
                        | MouseButtonPressed MouseButtonType.Right ->

                            let (playerEntity, _) = world.EntityQuery.GetComponents<Player> () |> Seq.head

                            match world.EntityQuery.TryGetComponent<Camera> playerEntity with
                            | None -> ()                                    
                            | Some camera ->
                                camera.Position.Assign (Observable.Never ())
                                camera.PreviousPosition.Assign (Observable.Never ())

                        | MouseButtonPressed MouseButtonType.Left ->

                            let cameras = world.EntityQuery.GetComponents<Camera> ()

                            match cameras with
                            | [||] -> ()
                            | _ ->
            
                            let (_,camera) = cameras.[0]

                            let mouse = Input.getMousePosition()
                            let mouseV = Vector3 (single mouse.X, single mouse.Y, 0.f)

                            let v = unProject (mouseV, Matrix4x4.Identity, camera.View, camera.Projection, camera.ViewportPosition, camera.ViewportDimensions, camera.ViewportDepth)
                            let v = Vector2 (v.X, v.Y)

                            let n = !count
                            count := n + 1

                            let comps : IComponent list =
                                let data =
                                    [|
                                        Vector2 (1.127f, 1.77f)
                                        Vector2 (0.f, 1.77f)
                                        Vector2 (0.f, 0.f)
                                        Vector2 (1.127f, 0.f)
                                    |]

                                let position = 
                                    { 
                                        Position.Var = Var.create v
                                    }

                                let rotation = { Rotation.Var = Var.create 0.f }

                                let physicsPolygon =
                                    {
                                        Data = data
                                        Density = 1.f
                                        Restitution = 0.f
                                        Friction = 1.f
                                        Mass = 1.f
                                        IsStatic = false
                                        Body = null
                                        PolygonShape = null
                                        Fixture = null
                                    }

                                let render =
                                    {
                                        R = 0uy
                                        G = 0uy
                                        B = 0uy
                                        VBO = Renderer.R.CreateVBO(data)
                                        Position = Val.create Vector2.Zero
                                        PreviousPosition = Val.create Vector2.Zero
                                        Rotation = Val.create 0.f
                                        PreviousRotation = Val.create 0.f
                                    }

                                [position;rotation;physicsPolygon;render]

                            world.EntityFactory.CreateActive n comps     
                                                   
                        | _ -> ()
                    )
                )



            )

        member __.Update world =
            ()

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

    let world = World (20480)

    world.AddSystem inputSystem
    world.AddSystem movementSystem
    world.AddSystem physicsSystem

    let rendererSystem : ISystem = RendererSystem () :> ISystem
    rendererSystem.Init world

    let comps : IComponent list =
        let data =
            [|
                Vector2 (1.127f, 1.77f)
                Vector2 (0.f, 1.77f)
                Vector2 (0.f, 0.f)
                Vector2 (1.127f, 0.f)
            |]

        let position = 
            { 
                Position.Var = Var.create Vector2.Zero
            }

        let centroid =
            {
                Centroid.Var = Var.create Vector2.Zero
            }

        let rotation = { Rotation.Var = Var.create 0.f }

        let physicsPolygon =
            {
                Data = data
                Density = 1.f
                Restitution = 0.f
                Friction = 1.f
                Mass = 1.f
                IsStatic = false
                Body = null
                PolygonShape = null
                Fixture = null
            }

        let render =
            {
                R = 0uy
                G = 0uy
                B = 0uy
                VBO = Renderer.R.CreateVBO(data)
                Position = Val.create Vector2.Zero
                PreviousPosition = Val.create Vector2.Zero
                Rotation = Val.create 0.f
                PreviousRotation = Val.create 0.f
            }

        let camera =
            {
                Projection = Matrix4x4.CreateOrthographic (1280.f / 64.f, 720.f / 64.f, 0.1f, 1.f)
                View = Matrix4x4.Identity
                ViewportPosition = Vector2(0.f, 0.f)
                ViewportDimensions = Vector2(1280.f, 720.f)
                ViewportDepth = Vector2(0.1f, 1.f)
                Position = Val.create Vector2.Zero
                PreviousPosition = Val.create Vector2.Zero
            }
        
        [position;centroid;rotation;Player();physicsPolygon;render;camera]

    world.EntityFactory.CreateActive 0 comps


    let comps : IComponent list =
        let data =
            [|
                Vector2 (-1000.f, -1.f)
                Vector2 (1000.f, -1.f)
                Vector2 (1000.f, 1.f)
                Vector2 (-1000.f, 1.f)
            |]

        let positionValue = Vector2 (0.f, -10.f)

        let position = 
            { 
                Position.Var = Var.create Vector2.Zero
            }

        let rotation = { Rotation.Var = Var.create 0.f }

        let physicsPolygon =
            {
                Data = data
                Density = 1.f
                Restitution = 0.f
                Friction = 1.f
                Mass = 1.f
                IsStatic = true
                Body = null
                PolygonShape = null
                Fixture = null
            }

        let render =
            {
                R = 0uy
                G = 0uy
                B = 0uy
                VBO = Renderer.R.CreateVBO(data)
                Position = Val.create Vector2.Zero
                PreviousPosition = Val.create Vector2.Zero
                Rotation = Val.create 0.f
                PreviousRotation = Val.create 0.f
            }

        [position;rotation;physicsPolygon;render]

    world.EntityFactory.CreateActive 1 comps


    let comps : IComponent list =
        let data =
            [|
                Vector2 (-50.f, 0.6858f)
                Vector2 (0.f, 0.6858f)
                Vector2 (0.f, 0.f)
                Vector2 (-50.f, 0.f)
            |]

        let positionValue = Vector2 (-5.f, 0.f)

        let position = 
            { 
                Position.Var = Var.create positionValue
            }

        let rotation = { Rotation.Var = Var.create 0.f }

        let physicsPolygon =
            {
                Data = data
                Density = 1.f
                Restitution = 0.f
                Friction = 1.f
                Mass = 1.f
                IsStatic = false
                Body = null
                PolygonShape = null
                Fixture = null
            }

        let render =
            {
                R = 0uy
                G = 0uy
                B = 0uy
                VBO = Renderer.R.CreateVBO(data)
                Position = Val.create Vector2.Zero
                PreviousPosition = Val.create Vector2.Zero
                Rotation = Val.create 0.f
                PreviousRotation = Val.create 0.f
            }

        [position;rotation;physicsPolygon;render]

    world.EntityFactory.CreateActive 2 comps

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


                world.Time.Value <- TimeSpan.FromTicks time
                world.Interval <- TimeSpan.FromTicks interval
                world.Run ()

                stopwatch.Stop ()
        )
        (
            fun delta world ->
                world.Delta <- delta
                rendererSystem.Update world
                Console.Clear ()

                //printfn "FPS: %.2f" (1000.f / single stopwatch.ElapsedMilliseconds)
                printfn "Update MS: %A" stopwatch.ElapsedMilliseconds
        )

    0