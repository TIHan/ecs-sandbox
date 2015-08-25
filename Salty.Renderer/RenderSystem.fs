namespace Salty.Renderer

open ECS.Core

open Salty.Core.Components
open Salty.Renderer.Components

open System
open System.Numerics
open System.Reactive.Linq

type RendererSystem () =
    let mutable context = Renderer.RendererContext ()
    let mutable vao = Renderer.VAO (0)
    let mutable defaultShader = 0

    let unProject (source: Vector3, model: Matrix4x4, view: Matrix4x4, projection: Matrix4x4, viewportPosition: Vector2, viewportDimensions: Vector2, viewportDepth: Vector2) =
        let _,m = Matrix4x4.Invert (model * view * projection)
        let x = (((source.X - viewportPosition.X) / (viewportDimensions.X)) * 2.f) - 1.f
        let y = -((((source.Y - viewportPosition.Y) / (viewportDimensions.Y)) * 2.f) - 1.f)
        let z = (source.Z - viewportDepth.X) / (viewportDepth.Y - viewportDepth.X)
        let mutable v = Vector3.Transform(Vector3 (x, y, z), m)
        v

    interface ISystem with

        member __.Init world =
            Renderer.R.InitSDL ()
            let window = Renderer.R.CreateWindow ()
            context <- Renderer.R.Init (window)
            vao <- Renderer.R.CreateVao ()
            defaultShader <- Renderer.R.LoadShaders ("SimpleVertexShader.vertexshader", "SimpleFragmentShader.fragmentshader")

            World.componentAdded<Position> world
            |> Observable.add (function
                | (entity, position) ->
                    match world.ComponentQuery.TryGet<Render> entity with
                    | Some render ->
                        render.Position.Assign position.Var
                    | _ -> ()
            )

            World.componentAdded<Rotation> world
            |> Observable.add (function
                | (entity, rotation) ->
                    match world.ComponentQuery.TryGet<Render> entity with
                    | Some render ->
                        render.Rotation.Assign rotation.Var
                    | _ -> ()
            )

            world.Time.Current
            |> Observable.add (fun _ ->
                world.ComponentQuery.ForEach<Camera> (fun (_, camera) ->
                    camera.PreviousPosition <- camera.Position.Value
                    camera.PreviousProjection <- camera.Projection
                )

                world.ComponentQuery.ForEach<Render> (fun (_, render) ->
                    render.PreviousPosition <- render.Position.Value
                    render.PreviousRotation <- render.Rotation.Value
                )
            )

        member __.Update world =
            let delta = world.Time.Delta.Value

            Renderer.R.Clear ()

            match world.ComponentQuery.TryFind<Camera> (fun _ -> true) with
            | None -> ()
            | Some (_,camera) ->

                let projection = Matrix4x4.Lerp (camera.PreviousProjection, camera.Projection, delta)
                let view = ref camera.View

                let value = Vector2.Lerp (camera.PreviousPosition, camera.Position.Value, delta)
                view := Matrix4x4.CreateTranslation (Vector3 (value, 0.f) * -1.f)

                camera.View <- !view

                Renderer.R.UseProgram defaultShader
                Renderer.R.SetProjection defaultShader projection
                Renderer.R.SetView defaultShader !view

                world.ComponentQuery.ForEach<Render> (fun (entity, render) ->
                    let position = render.Position.Value
                    let previousPosition = render.PreviousPosition
                    let rotation = render.Rotation.Value
                    let previousRotation = render.PreviousRotation
                    let delta = world.Time.Delta.Value

                    let positionValue = Vector2.Lerp (previousPosition, position, delta)
                    let rotationValue = Vector2.Lerp(Vector2 (previousRotation, 0.f), Vector2 (rotation, 0.f), delta).X

                    let rotationMatrix = Matrix4x4.CreateRotationZ (rotationValue)
                    let model = rotationMatrix * Matrix4x4.CreateTranslation (Vector3 (positionValue, 0.f))

                    Renderer.R.SetModel defaultShader model

                    Renderer.R.DrawLineLoop defaultShader render.VBO
                )

                Renderer.R.Draw (context)