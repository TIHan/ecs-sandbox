open System
open System.IO
open System.Diagnostics
open System.Collections.Generic
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

open ECS

#nowarn "9"
#nowarn "51"

open System.Numerics

type Centroid =
    {
        PreviousValue: Vector2 ref
        Value: Vector2 ref
    }

type Position =
    {
        PreviousValue: Vector2 ref
        Value: Vector2 ref
        Centroid: Centroid
    }

type Rotation =
    {
        PreviousValue: single ref
        Value: single ref
    }

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

type Player = Player of unit

type Render =
    {
        R: byte
        G: byte
        B: byte
        VBO: Renderer.VBO
        PositionRef: ComponentRef<Position>
        RotationRef: ComponentRef<Rotation>
    }

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


type PhysicsEvent =
    | Created of Entity * PhysicsPolygon

type PhysicsSystem () =
    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))
    interface ISystem with
        
        member __.Init world =
            world.HandleEvent<PhysicsEvent> (fun observable ->
                observable
                |> Observable.add (function
                    | Created (entity, physicsPolygon) ->
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
                                    printfn "%A" fixture1.UserData
                                    printfn "%A" fixture2.UserData
                                    true
                            )
                )
            )

        member __.Update world =
            world.Query.ForEachActiveEntityComponent<PhysicsPolygon, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                physicsPolygon.Body.Position <- !position.Value
                physicsPolygon.Body.Rotation <- !rotation.Value
                physicsPolygon.Body.Awake <- true
            )

            physicsWorld.Step (single world.Interval.TotalSeconds)

            world.Query.ForEachActiveEntityComponent<PhysicsPolygon, Position, Rotation> (fun (entity, physicsPolygon, position, rotation) ->
                position.PreviousValue := !position.Value
                position.Value := physicsPolygon.Body.Position

                position.Centroid.PreviousValue := !position.Centroid.Value
                position.Centroid.Value := physicsPolygon.Body.WorldCenter

                rotation.PreviousValue := !rotation.Value
                rotation.Value := physicsPolygon.Body.Rotation
            )

///////////////////////////////////////////////////////////////////

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

        member __.Update world =
            Renderer.R.Clear ()

            let projection = Matrix4x4.CreateOrthographic (1280.f / 64.f, 720.f / 64.f, 0.1f, Single.MaxValue)
            let view = ref Matrix4x4.Identity

            world.Query.ForEachActiveEntityComponent<Player, Position> (fun (entity, _, position) ->
                let centroid = position.Centroid
                let value = Vector2.Lerp (!centroid.PreviousValue, !centroid.Value, world.Delta)
                view := Matrix4x4.CreateTranslation (Vector3 (value, 0.f) * -1.f)
            )

            Renderer.R.UseProgram defaultShader
            Renderer.R.SetProjection defaultShader projection
            Renderer.R.SetView defaultShader !view

            world.Query.ForEachActiveEntityComponent<Render, Position> (fun (entity, render, position) ->
                let position = render.PositionRef.Value
                let rotation = render.RotationRef.Value

                let positionValue = Vector2.Lerp (!position.PreviousValue, !position.Value, world.Delta)
                let rotationValue = Vector2.Lerp(Vector2 (!rotation.PreviousValue, 0.f), Vector2 (!rotation.Value, 0.f), world.Delta).X

                let rotationMatrix = Matrix4x4.CreateRotationZ (rotationValue)
                let model = rotationMatrix * Matrix4x4.CreateTranslation (Vector3 (positionValue, 0.f))

                Renderer.R.SetModel defaultShader model
                Renderer.R.DrawLineLoop defaultShader render.VBO
            )

            Renderer.R.Draw (context)



open Input
open Nessos.FsPickler

type InputSystem () =
    let binary = FsPickler.CreateBinary()
    let recordings = Dictionary<TimeSpan, InputEvent list> ()

    interface ISystem with

        member __.Init world =
            ()

        member __.Update world =

            match world.Time.Seconds > 5 with
            | true ->
                use fileStream = File.Open ("recordings.bin", FileMode.OpenOrCreate)

                binary.Serialize (fileStream, recordings)
                fileStream.Close ()
                fileStream.Dispose ()

                let fileStream = File.Open ("recordings.bin", FileMode.Open)
                let dict : (Dictionary<TimeSpan, InputEvent list>) = binary.Deserialize (fileStream)

                match dict.TryGetValue world.Time with
                | true, events -> world.RaiseEvent<InputEvent list> (events)
                | _ -> ()  

            | _ -> ()

            let events = Input.getEvents ()

            world.RaiseEvent<InputEvent list> (events)

            recordings.Add (world.Time, events)

type PlaybackSystem () =
    let fileStream = File.Open ("recordings.bin", FileMode.Open)
    let binary = FsPickler.CreateBinary()
    let dict : (Dictionary<TimeSpan, InputEvent list>) = binary.Deserialize<Dictionary<TimeSpan, InputEvent list>> (fileStream)

    interface ISystem with

        member __.Init world =
            ()

        member __.Update world =
            match dict.TryGetValue (world.Time - TimeSpan.FromTicks(world.Interval.Ticks * 273L)) with
            | true, events -> world.RaiseEvent<InputEvent list> (events)
            | _ -> ()  

type MovementSystem () =
    interface ISystem with
        
        member __.Init world =
            world.HandleEvent<InputEvent list> (fun observable ->
                observable
                |> Observable.add (fun events ->

                    world.Query.ForEachActiveEntityComponent<Player, PhysicsPolygon> (fun (entity, _, physicsPolygon) ->

                        events
                        |> List.iter (function
                            | JoystickButtonPressed 2 -> 
                                physicsPolygon.Body.ApplyForce (Vector2.UnitX * -20.f)
                            | JoystickButtonPressed 3 -> 
                                physicsPolygon.Body.ApplyForce (Vector2.UnitX * 20.f)
                            | JoystickButtonPressed 10 -> 
                                physicsPolygon.Body.ApplyForce (Vector2.UnitY * 50.0f)
                            | _ -> ()
                        )
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

    //let inputSystem = InputSystem ()
    let playbackSystem = PlaybackSystem ()
    let movementSystem = MovementSystem ()
    let physicsSystem = PhysicsSystem ()

    let world = World (20480)

    //world.AddSystem inputSystem
    world.AddSystem playbackSystem
    world.AddSystem movementSystem
    world.AddSystem physicsSystem

    let rendererSystem : ISystem = RendererSystem () :> ISystem
    rendererSystem.Init world

    world.CreateActiveEntity 0 (fun entity ->

        let data =
            [|
                Vector2 (1.127f, 1.77f)
                Vector2 (0.f, 1.77f)
                Vector2 (0.f, 0.f)
                Vector2 (1.127f, 0.f)
            |]

        let position = 
            { 
                Position.Value = ref Vector2.Zero
                PreviousValue = ref Vector2.Zero
                Centroid = 
                    {
                        Value = ref Vector2.Zero
                        PreviousValue = ref Vector2.Zero
                    }
            }

        world.SetEntityComponent position entity

        let rotation = { Rotation.Value = ref 0.f; PreviousValue = ref 0.f }

        world.SetEntityComponent rotation entity

        world.SetEntityComponent (Player ()) entity

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

        world.SetEntityComponent physicsPolygon entity

        world.RaiseEvent<PhysicsEvent> (PhysicsEvent.Created (entity, physicsPolygon))

        let render =
            {
                R = 0uy
                G = 0uy
                B = 0uy
                VBO = Renderer.R.CreateVBO(data)
                PositionRef = world.Query.GetComponentRef<Position> entity
                RotationRef = world.Query.GetComponentRef<Rotation> entity
            }

        world.SetEntityComponent render entity
    )


    world.CreateActiveEntity 1 (fun entity ->

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
                Position.Value = ref positionValue
                PreviousValue = ref positionValue
                Centroid = 
                    {
                        Value = ref Vector2.Zero
                        PreviousValue = ref Vector2.Zero
                    }
            }

        world.SetEntityComponent position entity

        let rotation = { Rotation.Value = ref 0.f; PreviousValue = ref 0.f }

        world.SetEntityComponent rotation entity

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

        world.SetEntityComponent physicsPolygon entity

        world.RaiseEvent<PhysicsEvent> (PhysicsEvent.Created (entity, physicsPolygon))

        let render =
            {
                R = 0uy
                G = 0uy
                B = 0uy
                VBO = Renderer.R.CreateVBO(data)
                PositionRef = world.Query.GetComponentRef<Position> entity
                RotationRef = world.Query.GetComponentRef<Rotation> entity
            }

        world.SetEntityComponent render entity
    )



    world.CreateActiveEntity 2 (fun entity ->

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
                Position.Value = ref positionValue
                PreviousValue = ref positionValue
                Centroid = 
                    {
                        Value = ref Vector2.Zero
                        PreviousValue = ref Vector2.Zero
                    }
            }

        world.SetEntityComponent position entity

        let rotation = { Rotation.Value = ref 0.f; PreviousValue = ref 0.f }

        world.SetEntityComponent rotation entity

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

        world.SetEntityComponent physicsPolygon entity

        world.RaiseEvent<PhysicsEvent> (PhysicsEvent.Created (entity, physicsPolygon))

        let render =
            {
                R = 0uy
                G = 0uy
                B = 0uy
                VBO = Renderer.R.CreateVBO(data)
                PositionRef = world.Query.GetComponentRef<Position> entity
                RotationRef = world.Query.GetComponentRef<Rotation> entity
            }

        world.SetEntityComponent render entity
    )


    GameLoop.start
        world
        30.
        (
            fun () ->
                GC.Collect 0
        )
        (
            fun time interval world ->
                Input.clearEvents ()
                Input.pollEvents ()

                world.Time <- TimeSpan.FromTicks time
                world.Interval <- TimeSpan.FromTicks interval
                world.Run ()
        )
        (
            fun delta world ->
                world.Delta <- delta
                rendererSystem.Update world
        )

    0