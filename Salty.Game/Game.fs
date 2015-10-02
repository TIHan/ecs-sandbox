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

type Health () =

    member val Var = Var.create 0.f with get

    interface IComponent

[<RequireQualifiedAccess>]
module EntityBlueprint =

    let test p (blueprint: EntityBlueprint) =
        blueprint
        |> EntityBlueprint.add (fun () ->
            let position = Position ()
            __unsafe.setVarValueWithNotify position.Var p
            position
        )
        |> EntityBlueprint.add (fun () ->
            let rotation = Rotation ()
            __unsafe.setVarValueWithNotify rotation.Var 0.f
            rotation
        )
        |> EntityBlueprint.add (fun () ->
            let health = Health ()
            __unsafe.setVarValueWithNotify health.Var 100.f
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
            __unsafe.setVarValueWithNotify position.Var p
            position
        )
        |> EntityBlueprint.add (fun () ->
            let rotation = Rotation ()
            __unsafe.setVarValueWithNotify rotation.Var 0.f
            rotation
        )
        |> EntityBlueprint.add (fun () ->
            let health = Health ()
            __unsafe.setVarValueWithNotify health.Var 0.f
            health
        )
        |> EntityBlueprint.add (fun () ->
            let physics = Physics ()
            __unsafe.setVarValueWithNotify physics.Data data
            __unsafe.setVarValueWithNotify physics.Density 1.f
            __unsafe.setVarValueWithNotify physics.Restitution 0.1f
            __unsafe.setVarValueWithNotify physics.Friction 0.1f
            __unsafe.setVarValueWithNotify physics.Mass 1.f
            __unsafe.setVarValueWithNotify physics.IsStatic false
            physics
        )
        |> EntityBlueprint.add (fun () ->
            let render = Render ()
            let obs = Observable.StartWith (Observable.Never (), [|data|])
            render.Shader <- Some <| Shader ("boxTexture.vsh", "boxTexture.fsh")
            render.Texture <- Some <| Texture ("crate.jpg", uvData)
            render.DrawKind <- DrawKind.Triangles
            __unsafe.setValSource render.Data obs
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
            __unsafe.setVarValueWithNotify position.Var positionValue
            position
        )
        |> EntityBlueprint.add (fun () ->
            let rotation = Rotation ()
            __unsafe.setVarValueWithNotify rotation.Var 0.f
            rotation
        )
        |> EntityBlueprint.add (fun () ->
            let physics = Physics ()
            __unsafe.setVarValueWithNotify physics.Data data
            __unsafe.setVarValueWithNotify physics.Density 1.f
            __unsafe.setVarValueWithNotify physics.Restitution 0.f
            __unsafe.setVarValueWithNotify physics.Friction 0.1f
            __unsafe.setVarValueWithNotify physics.Mass 1.f
            __unsafe.setVarValueWithNotify physics.IsStatic true
            physics
        )
        |> EntityBlueprint.add (fun () ->
            let render = Render ()
            let obs = Observable.StartWith (Observable.Never (), [|data|])
            render.Shader <- Some <| Shader ("boxLines.vsh", "boxLines.fsh")
            __unsafe.setValSource render.Data obs
            render.R <- 120uy
            render.G <- 120uy
            render.B <- 120uy
            render
        )
