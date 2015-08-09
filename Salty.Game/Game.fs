namespace Salty.Game

open ECS.Core

open System.Numerics

open Salty.Core.Components
open Salty.Input
open Salty.Input.Components
open Salty.Physics
open Salty.Physics.Components
open Salty.Renderer
open Salty.Renderer.Components

type PlayerCommand =
    | StartMovingUp = 0
    | StopMovingUp = 1

type Player () =

    member val IsMovingUp = Var.create false

    interface IComponent<Player>

[<RequireQualifiedAccess>]
module Game =

    let box p (desc: EntityDescription) =
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

        let physics = Physics ()
        physics.Data.Value <- data
        physics.Density.Value <- 1.f
        physics.Restitution.Value <- 0.f
        physics.Friction.Value <- 1.f
        physics.Mass.Value <- 1.f
        physics.IsStatic.Value <- false

        let render = Render ()
        render.VBO <- Renderer.R.CreateVBO (data)

        desc
        |> Entity.add position
        |> Entity.add rotation
        |> Entity.add physics
        |> Entity.add render

    let player position desc =
        box position desc
        |> Entity.add (Player ())
        |> Entity.add (Input ())

    let camera (desc: EntityDescription) =
        let camera = Camera ()
        camera.Projection <- Matrix4x4.CreateOrthographic (1280.f / 64.f, 720.f / 64.f, 0.1f, 1.f)
        camera.ViewportDimensions <- Vector2 (1280.f, 720.f)
        camera.ViewportDepth <- Vector2 (0.1f, 1.f)

        desc |> Entity.add camera

    let staticBox desc =
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

        desc
        |> Entity.add position
        |> Entity.add rotation
        |> Entity.add physics
        |> Entity.add render
