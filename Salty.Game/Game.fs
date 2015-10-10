namespace Salty.Game

open ECS.Core
open Salty.Core
open Salty.Core.Components
open Salty.Core.Physics
open Salty.Core.Physics.Components
open Salty.Core.Renderer
open Salty.Core.Renderer.Components

open Salty.Game.Core.Components

open System.Numerics
open System.Reactive.Linq

// TODO: Fields will be private.
type Health =
    {
        Value: Var<int>
        MaxValue: int
        IsDead: Var<bool>
    }

    static member Default =
        {
            Value = Var.create 0
            MaxValue = 0
            IsDead = Var.create false
        }

    interface IComponent

[<AutoOpen>]
module Game =

    module Health =

        let create value maxValue =
            {
                Value = Var.create value
                MaxValue = maxValue
                IsDead = Var.create false
            }

        let value (health: Health) =
            Val.ofVar health.Value

        let maxValue (health: Health) = health.MaxValue

        let isDead (health: Health) =
            Val.ofVar health.IsDead

        let damage (healthToLose: int) (health: Health) (_: IWorld<Salty>) =
            if not !!health.IsDead then
                let value = !!health.Value
                let newValue = value - healthToLose

                health.Value <-- newValue

                if newValue <= 0 then
                    health.IsDead <-- true

        let gain (healthToGain: int) (health: Health) (_: IWorld<Salty>) =
            if not !!health.IsDead then
                health.Value <-- !!health.Value + healthToGain

[<RequireQualifiedAccess>]
module EntityBlueprint =

    let test p (blueprint: EntityBlueprint) =
        blueprint
        |> EntityBlueprint.add (fun () ->
            let position = Position ()
            position.Var.Value <- p
            position
        )
        |> EntityBlueprint.add (fun () ->
            let rotation = Rotation ()
            rotation.Var.Value <- 0.f
            rotation
        )
        |> EntityBlueprint.add (fun () ->
            let health = Health.create 100 100
            health.Value.Value <- 100
            health
        )

    let box p (blueprint: EntityBlueprint) =
//        let data =
//            [|
//                Vector2 (-1.f, -1.f)
//                Vector2 (-1.f, 1.f)
//                Vector2 (1.f, 1.f)
//                Vector2 (1.f, -1.f)
//            |]

        let data =
            [|
                Vector2 (-0.5f, -0.5f)
                Vector2 (-0.5f, 0.5f)
                Vector2 (0.5f, 0.5f)

                Vector2 (0.5f, 0.5f)
                Vector2 (0.5f, -0.5f)
                Vector2 (-0.5f, -0.5f)
            |]

        let uvData =
            [|
                Vector2 (0.f, 0.f)
                Vector2 (0.f, 1.f)
                Vector2 (1.f, 1.f)

                Vector2 (1.f, 1.f)
                Vector2 (1.f, 0.f)
                Vector2 (0.f, 0.f)
            |]
            |> Array.map (fun v -> 
                Vector2 (v.X, 1.f - v.Y)
            )

        blueprint
        |> EntityBlueprint.add (fun () ->
            let position = Position ()
            position.Var.Value <- p
            position
        )
        |> EntityBlueprint.add (fun () ->
            let rotation = Rotation ()
            rotation.Var.Value <- 0.f
            rotation
        )
        |> EntityBlueprint.add (fun () ->
            let health = Health.create 0 0
            health.Value.Value <- 0
            health
        )
        |> EntityBlueprint.add (fun () ->
            let physics = Physics (data)
            physics.Density.Value <- 1.f
            physics.Restitution.Value <- 0.1f
            physics.Friction.Value <- 0.1f
            physics.Mass.Value <- 1.f
            physics.IsStatic.Value <- false
            physics
        )
        |> EntityBlueprint.add (fun () ->
            let render = Render ()
            let obs = Observable.StartWith (Observable.Never (), [|data|])
            render.Shader <- Some <| Shader ("boxTexture.vsh", "boxTexture.fsh")
            render.Texture <- Some <| Texture ("crate.jpg", uvData)
            render.DrawKind <- DrawKind.Triangles
            render.Data.Listen obs
            render.G <- 255uy
            render
        )

    let player position desc =
        box position desc
        |> EntityBlueprint.add (fun () -> Player ())

    let camera (blueprint: EntityBlueprint) =
        blueprint 
        |> EntityBlueprint.add (fun () ->
            let camera = Camera ()
            camera.Projection <- Matrix4x4.CreateOrthographic (1280.f / 64.f, 720.f / 64.f, 0.1f, 1.f)
            camera.ViewportDimensions <- Vector2 (1280.f, 720.f)
            camera.ViewportDepth <- Vector2 (0.1f, 1.f)
            camera
        )

    let staticBox blueprint =
        let data =
            [|
                Vector2 (-1000.f, -1.f)
                Vector2 (1000.f, -1.f)
                Vector2 (1000.f, 1.f)
                Vector2 (-1000.f, 1.f)
            |]

        let positionValue = Vector2 (0.f, -2.f)

        blueprint
        |> EntityBlueprint.add (fun () ->
            let position = Position ()
            position.Var.Value <- positionValue
            position
        )
        |> EntityBlueprint.add (fun () ->
            let rotation = Rotation ()
            rotation.Var.Value <- 0.f
            rotation
        )
        |> EntityBlueprint.add (fun () ->
            let physics = Physics (data)
            physics.Density.Value <- 1.f
            physics.Restitution.Value <- 0.f
            physics.Friction.Value <- 0.1f
            physics.Mass.Value <- 1.f
            physics.IsStatic.Value <- true
            physics
        )
        |> EntityBlueprint.add (fun () ->
            let render = Render ()
            let obs = Observable.StartWith (Observable.Never (), [|data|])
            render.Shader <- Some <| Shader ("boxLines.vsh", "boxLines.fsh")
            render.Data.Listen obs
            render.R <- 120uy
            render.G <- 120uy
            render.B <- 120uy
            render
        )
