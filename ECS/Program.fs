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


type Position =
    {
        PreviousValue: Vector2 ref
        Value: Vector2 ref
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

type Friend = Friend of unit

type Enemy = Enemy of unit

type LocalHost = LocalHost of unit

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
        mutable Body: FarseerPhysics.Dynamics.Body
        mutable PolygonShape: FarseerPhysics.Collision.Shapes.PolygonShape
        mutable Fixture: FarseerPhysics.Dynamics.Fixture
    }


type PhysicsEvent =
    | Created of int * PhysicsPolygon

type PhysicsSystem () =
    let physicsWorld = FarseerPhysics.Dynamics.World (Vector2(0.f, -9.820f))
    interface ISystem with
        
        member __.Init world =
            world.HandleEvent<PhysicsEvent> (fun observable ->
                observable
                |> Observable.add (function
                    | Created (entityId, physicsPolygon) ->
                        let data = 
                            physicsPolygon.Data

                        physicsPolygon.Body <- new FarseerPhysics.Dynamics.Body (physicsWorld)
                        physicsPolygon.Body.BodyType <- if physicsPolygon.IsStatic then FarseerPhysics.Dynamics.BodyType.Static else FarseerPhysics.Dynamics.BodyType.Dynamic
                        physicsPolygon.Body.Restitution <- 0.f
                        physicsPolygon.Body.Friction <- 1.f
                        physicsPolygon.Body.Mass <- 1.f
                        physicsPolygon.PolygonShape <- new FarseerPhysics.Collision.Shapes.PolygonShape (FarseerPhysics.Common.Vertices (data), physicsPolygon.Density)
                        physicsPolygon.Fixture <- physicsPolygon.Body.CreateFixture (physicsPolygon.PolygonShape)
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

            let projection = Matrix4x4.CreateOrthographic (1280.f / 128.f, 720.f / 128.f, 0.1f, Single.MaxValue)
            let view = ref Matrix4x4.Identity

            world.Query.ForEachActiveEntityComponent<LocalHost, PhysicsPolygon, Position> (fun (entity, _, physicsPolygon, position) ->
                let value = Vector3 (physicsPolygon.Body.WorldCenter, 0.f)
                view := Matrix4x4.CreateTranslation (value * -1.f)
            )

            Renderer.R.UseProgram defaultShader
            Renderer.R.SetProjection defaultShader projection
            Renderer.R.SetView defaultShader !view

            world.Query.ForEachActiveEntityComponent<Render> (fun (entity, render) ->
                if render.PositionRef.IsNull
                then ()
                else
                    let position = render.PositionRef.Value
                    let rotation = render.RotationRef.Value

                    let positionValue = Vector3.Lerp (Vector3 (!position.PreviousValue, 0.f), Vector3 (!position.Value, 0.f), world.Delta)
                    let rotationValue = Vector2.Lerp(Vector2 (!rotation.PreviousValue, 0.f), Vector2 (!rotation.Value, 0.f), world.Delta).X

                    let rotationMatrix = Matrix4x4.CreateRotationZ (rotationValue)
                    let model = rotationMatrix * Matrix4x4.CreateTranslation (positionValue)

                    Renderer.R.SetModel defaultShader model
                    Renderer.R.DrawLineLoop defaultShader render.VBO

            )

            Renderer.R.Draw (context)



open Input

type MovementSystem () =
    interface ISystem with
        
        member __.Init world =
            ()

        member __.Update world =
            world.Query.ForEachActiveEntityComponent<LocalHost, PhysicsPolygon> (fun (entity, _, physicsPolygon) ->
                let events = Input.getEvents ()

                if Input.isJoystickButtonPressed 2 then
                    physicsPolygon.Body.ApplyForce (Vector2.UnitX * -2.2f)

                if Input.isJoystickButtonPressed 3 then
                    physicsPolygon.Body.ApplyForce (Vector2.UnitX * 2.2f)

                match events |> List.tryFind (function
                    | JoystickButtonPressed 10 -> true
                    | _ -> false) with
                | Some _ ->
                    physicsPolygon.Body.ApplyForce (Vector2.UnitY * 50.0f)
                | _ -> ()
            )

///////////////////////////////////////////////////////////////////

let benchmark f =
    let s = Stopwatch.StartNew ()
    f ()
    s.Stop()
    printfn "MS: %A" s.ElapsedMilliseconds

[<EntryPoint>]
let main argv = 

    let movementSystem = MovementSystem ()
    let physicsSystem = PhysicsSystem ()

    let world = World (20480)

    world.AddSystem movementSystem
    world.AddSystem physicsSystem

    let rendererSystem : ISystem = RendererSystem () :> ISystem
    rendererSystem.Init world

    world.CreateActiveEntity 0 (fun entity ->

        let data =
            [|
                Vector2 (0.127f, 1.77f)
                Vector2 (0.f, 1.77f)
                Vector2 (0.f, 0.f)
                Vector2 (0.127f, 0.f)
            |]

        let position = { Position.Value = ref <| Vector2.Zero; PreviousValue = ref <| Vector2.Zero }

        world.SetEntityComponent position entity

        let rotation = { Rotation.Value = ref 0.f; PreviousValue = ref 0.f }

        world.SetEntityComponent rotation entity

        world.SetEntityComponent (LocalHost ()) entity

        let physicsPolygon =
            {
                Data = data
                Density = 1.f
                IsStatic = false
                Body = null
                PolygonShape = null
                Fixture = null
            }

        world.SetEntityComponent physicsPolygon entity

        world.RaiseEvent<PhysicsEvent> (PhysicsEvent.Created (entity.Id, physicsPolygon))

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

        let position = { Position.Value = ref positionValue; PreviousValue = ref positionValue }

        world.SetEntityComponent position entity

        let rotation = { Rotation.Value = ref 0.f; PreviousValue = ref 0.f }

        world.SetEntityComponent rotation entity

        let physicsPolygon =
            {
                Data = data
                Density = 0.f
                IsStatic = true
                Body = null
                PolygonShape = null
                Fixture = null
            }

        world.SetEntityComponent physicsPolygon entity

        world.RaiseEvent<PhysicsEvent> (PhysicsEvent.Created (entity.Id, physicsPolygon))

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
                Vector2 (1.f, 0.6858f)
                Vector2 (0.f, 0.6858f)
                Vector2 (0.f, 0.f)
                Vector2 (1.f, 0.f)
            |]

        let position = Vector2 (-5.f, 0.f)

        let position = { Position.Value = ref position; PreviousValue = ref position }

        world.SetEntityComponent position entity

        let rotation = { Rotation.Value = ref 0.f; PreviousValue = ref 0.f }

        world.SetEntityComponent rotation entity

        let physicsPolygon =
            {
                Data = data
                Density = 1.f
                IsStatic = false
                Body = null
                PolygonShape = null
                Fixture = null
            }

        world.SetEntityComponent physicsPolygon entity

        world.RaiseEvent<PhysicsEvent> (PhysicsEvent.Created (entity.Id, physicsPolygon))

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