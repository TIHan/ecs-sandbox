namespace Salty.Game

open ECS.Core

open System.Numerics

open Salty.Core.Components
open Salty.Physics
open Salty.Physics.Components
open Salty.Renderer
open Salty.Renderer.Components

open Salty.Game.Core.Components

type Health () =

    member val Var = Var.create 0.f with get

    interface IComponent

[<RequireQualifiedAccess>]
module EntityBlueprint =

    let box p (blueprint: EntityBlueprint) =
        let data =
            [|
                Vector2 (1.f, 1.5f)
                Vector2 (0.f, 1.5f)
                Vector2 (0.f, 0.f)
                Vector2 (1.f, 0.f)
            |]

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
            let health = Health ()
            health.Var.Value <- 1000000.f
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
            render.VBO <- Renderer.R.CreateVBO (data)
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
            render.VBO <- Renderer.R.CreateVBO (data)
            render
        )
