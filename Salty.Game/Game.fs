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

type Health (maxHealth) =

    member val Value = Var.create 0.f with get

    member val MaxHealth = maxHealth

    interface IComponent

[<AutoOpen>]
module Game =

    module Player =

        let isDead (health: single) (isDead: bool) =
            isDead || health <= 0.f

        let canRessurect (maxHealth: single) (health: single) =
            health >= maxHealth && maxHealth > 0.f

    module Health =

        let update (maxHealth: single) (health: single) =
            if health >= maxHealth 
            then maxHealth
            else health

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
            let health = Health (100.f)
            health.Value.Value <- 100.f
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
            let health = Health (0.f)
            health.Value.Value <- 0.f
            health
        )
        |> EntityBlueprint.add (fun () ->
            let physics = Physics ()
            physics.Data.Value <- data
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
            let physics = Physics ()
            physics.Data.Value <- data
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
