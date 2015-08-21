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

        let position = Position ()
        position.Var.Value <- p

        let rotation = Rotation ()
        rotation.Var.Value <- 0.f

        let health = Health ()
        health.Var.Value <- 100.f

        let physics = Physics ()
        physics.Data.Value <- data
        physics.Density.Value <- 1.f
        physics.Restitution.Value <- 0.1f
        physics.Friction.Value <- 0.1f
        physics.Mass.Value <- 1.f
        physics.IsStatic.Value <- false

        let render = Render ()
        render.VBO <- Renderer.R.CreateVBO (data)

        blueprint
        |> EntityBlueprint.add position
        |> EntityBlueprint.add rotation
        |> EntityBlueprint.add health
        |> EntityBlueprint.add physics
        |> EntityBlueprint.add render

    let player position desc =
        box position desc
        |> EntityBlueprint.add (Player ())

    let camera (blueprint: EntityBlueprint) =
        let camera = Camera ()
        camera.Projection <- Matrix4x4.CreateOrthographic (1280.f / 64.f, 720.f / 64.f, 0.1f, 1.f)
        camera.ViewportDimensions <- Vector2 (1280.f, 720.f)
        camera.ViewportDepth <- Vector2 (0.1f, 1.f)

        blueprint |> EntityBlueprint.add camera

    let staticBox blueprint =
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

        let physics = Physics ()
        physics.Data.Value <- data
        physics.Density.Value <- 1.f
        physics.Restitution.Value <- 0.f
        physics.Friction.Value <- 1.f
        physics.Mass.Value <- 1.f
        physics.IsStatic.Value <- true

        let render = Render ()
        render.VBO <- Renderer.R.CreateVBO (data)

        blueprint
        |> EntityBlueprint.add position
        |> EntityBlueprint.add rotation
        |> EntityBlueprint.add physics
        |> EntityBlueprint.add render
