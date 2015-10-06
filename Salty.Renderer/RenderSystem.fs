namespace Salty.Core.Renderer

open ECS.Core
open Salty.Core
open Salty.Core.Components
open Salty.Core.Renderer.Components

open System
open System.Numerics
open System.Reactive.Linq

type RendererSystem () =
    let mutable context = Renderer.RendererContext ()
    let mutable vao = Renderer.VAO (0)

    let unProject (source: Vector3, model: Matrix4x4, view: Matrix4x4, projection: Matrix4x4, viewportPosition: Vector2, viewportDimensions: Vector2, viewportDepth: Vector2) =
        let _,m = Matrix4x4.Invert (model * view * projection)
        let x = (((source.X - viewportPosition.X) / (viewportDimensions.X)) * 2.f) - 1.f
        let y = -((((source.Y - viewportPosition.Y) / (viewportDimensions.Y)) * 2.f) - 1.f)
        let z = (source.Z - viewportDepth.X) / (viewportDepth.Y - viewportDepth.X)
        let mutable v = Vector3.Transform(Vector3 (x, y, z), m)
        v

    interface ISystem<Salty> with

        member __.Init world =
            Renderer.R.InitSDL ()
            let window = Renderer.R.CreateWindow ()
            context <- Renderer.R.Init (window)
            vao <- Renderer.R.CreateVao ()

            Component.added world
            |> Observable.add (fun (entity, render: Render) ->
                render.Data
                |> Observable.add (fun data ->
                    render.VBO <- Renderer.R.CreateVBO (data)
                )
            )

            world.Dependency.CurrentTime
            |> Observable.add (fun _ ->
                world.ComponentQuery.ForEach<Camera> (fun _ camera ->
                    camera.PreviousPosition <- camera.Position.Value
                    camera.PreviousProjection <- camera.Projection
                )

                world.ComponentQuery.ForEach<Render> (fun _ render ->
                    render.PreviousPosition <- render.Position.Value
                    render.PreviousRotation <- render.Rotation.Value
                )
            )

            (
                rule2 <| fun ent (render: Render) (position: Position) ->
                    render.PreviousPosition <- position.Var.Value
                    [
                        position.Var ==> render.Position
                    ]
            ) world

            (
                rule2 <| fun ent (render: Render) (rotation: Rotation) ->
                    render.PreviousRotation <- rotation.Var.Value
                    [
                        rotation.Var ==> render.Rotation
                    ]
            ) world

        member __.Update world =
            let delta = world.Dependency.DeltaTime.Value

            Renderer.R.Clear ()

            match world.ComponentQuery.TryFind<Camera> (fun _ _ -> true) with
            | None -> ()
            | Some (_,camera) ->

                let projection = Matrix4x4.Lerp (camera.PreviousProjection, camera.Projection, delta)
                let view = ref camera.View

                let value = Vector2.Lerp (camera.PreviousPosition, camera.Position.Value, delta)
                view := Matrix4x4.CreateTranslation (Vector3 (value, 0.f) * -1.f)

                camera.View <- !view

                world.ComponentQuery.ForEach<Render> (fun entity render ->
                    let position = render.Position.Value
                    let previousPosition = render.PreviousPosition
                    let rotation = render.Rotation.Value
                    let previousRotation = render.PreviousRotation

                    let positionValue = Vector2.Lerp (previousPosition, position, delta)
                    let rotationValue = Vector2.Lerp(Vector2 (previousRotation, 0.f), Vector2 (rotation, 0.f), delta).X

                    let rotationMatrix = Matrix4x4.CreateRotationZ (rotationValue)
                    let model = rotationMatrix * Matrix4x4.CreateTranslation (Vector3 (positionValue, 0.f))

                    match render.Shader with
                    | Some shader ->
                        if not shader.HasLoaded then shader.LoadShader ()

                        R.UseProgram shader.Id
                        R.SetProjection shader.Id projection
                        R.SetView shader.Id !view

                        R.SetModel shader.Id model
                        R.SetColor shader.Id (single render.R / 255.f) (single render.G / 255.f) (single render.B / 255.f)

                        match render.Texture with
                        | Some texture ->
                            if not texture.HasLoaded then texture.LoadTexture ()

                            R.SetTexture shader.Id texture.Id
                        | _ -> ()

                        match render.DrawKind with
                        | Lines -> Renderer.R.DrawLines shader.Id render.VBO
                        | LineLoop -> Renderer.R.DrawLineLoop shader.Id render.VBO
                        | Triangles ->

                            R.BindArrayBuffer render.VBO
                            let positionAttrib = R.BindAttribute shader.Id "position"

                            let uvAttrib =
                                match render.Texture with
                                | Some texture ->
                                    match texture.VBO with
                                    | Some vbo ->
                                        R.BindArrayBuffer vbo
                                        Some <| R.BindAttribute shader.Id "in_uv"
                                    | _ -> None
                                | _ -> None

                            ///
                            R.DrawTriangles render.VBO
                            ///

                            R.UnbindAttribute positionAttrib

                            match uvAttrib with
                            | Some uvAttrib -> R.UnbindAttribute uvAttrib
                            | _ -> ()

                            R.UnbindArrayBuffer ()
                    | _ -> ()
                )

                Renderer.R.Draw (context)