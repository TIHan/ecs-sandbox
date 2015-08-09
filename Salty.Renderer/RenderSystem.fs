﻿namespace Salty.Renderer

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

            World.componentAdded<Render> world
            |> Observable.add (function
                | (entity, comp) ->
                    comp.PreviousPosition.Assign (world.Time.Current.DistinctUntilChanged().Zip(comp.Position, fun _ x -> x))
                    comp.PreviousRotation.Assign (world.Time.Current.DistinctUntilChanged().Zip(comp.Rotation, fun _ x -> x))
            )

            World.componentAdded<Camera> world
            |> Observable.add (function
                | (entity, camera) ->
                    camera.PreviousPosition.Assign (world.Time.Current.DistinctUntilChanged().Zip(camera.Position, fun _ x -> x))
            )

            World.componentAdded<Position> world
            |> Observable.add (function
                | (entity, position) ->
                    match world.ComponentQuery.TryGet<Render> entity with
                    | Some render ->
                        render.Position.Assign position.Var
                        render.PreviousPosition.Assign (world.Time.Current.DistinctUntilChanged().Zip(render.Position, fun _ x -> x))
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

        member __.Update world =
            Renderer.R.Clear ()

            let cameras = world.ComponentQuery.Get<Camera> ()

            match cameras with
            | [||] -> ()
            | _ ->
            
            let (_,camera) = cameras.[0]
            let projection = camera.Projection
            let view = ref camera.View

            world.ComponentQuery.ForEach<Camera> (fun (_, camera) ->
                let value = Vector2.Lerp (camera.PreviousPosition.Value, camera.Position.Value, world.Time.Delta.Value)
                view := Matrix4x4.CreateTranslation (Vector3 (value, 0.f) * -1.f)
            )

            camera.View <- !view

            Renderer.R.UseProgram defaultShader
            Renderer.R.SetProjection defaultShader projection
            Renderer.R.SetView defaultShader !view

            world.ComponentQuery.ForEach<Render, Position> (fun (entity, render, position) ->
                let position = render.Position.Value
                let rotation = render.Rotation.Value

                let positionValue = Vector2.Lerp (render.PreviousPosition.Value, render.Position.Value, world.Time.Delta.Value)
                let rotationValue = Vector2.Lerp(Vector2 (render.PreviousRotation.Value, 0.f), Vector2 (render.Rotation.Value, 0.f), world.Time.Delta.Value).X

                let rotationMatrix = Matrix4x4.CreateRotationZ (rotationValue)
                let model = rotationMatrix * Matrix4x4.CreateTranslation (Vector3 (positionValue, 0.f))

                Renderer.R.SetModel defaultShader model

                Renderer.R.DrawLineLoop defaultShader render.VBO
            )

            Renderer.R.Draw (context)