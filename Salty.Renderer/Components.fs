namespace Salty.Renderer.Components

open ECS.Core

open System.Numerics

type Shader (vertexFilePath: string, fragmentFilePath: string) =

    member val internal HasLoaded = false with get, set

    member val internal Id = 0 with get, set

    member internal this.LoadShader () =
        match System.IO.File.Exists (vertexFilePath), System.IO.File.Exists (fragmentFilePath) with
        | true, true -> 
            this.Id <- Renderer.R.LoadShaders (vertexFilePath, fragmentFilePath)
            this.HasLoaded <- true
        | _ -> ()

type Texture (filePath: string, uvData: Vector2 []) =

    member val HasLoaded = false with get, set

    member val internal Id = 0 with get, set

    member internal this.UV = uvData

    member internal this.LoadTexture () =
        match System.IO.File.Exists (filePath) with
        | true ->
            this.Id <- Renderer.R.CreateTexture (filePath)
            this.HasLoaded <- true
        | _ -> ()

type DrawKind =
    | Lines
    | LineLoop
    | Triangles

type Camera () =

    member val Projection = Matrix4x4.Identity with get, set

    member val internal PreviousProjection = Matrix4x4.Identity with get, set

    member val View = Matrix4x4.Identity with get, set

    member val ViewportPosition = Vector2.Zero with get, set

    member val ViewportDimensions = Vector2.Zero with get, set

    member val ViewportDepth = Vector2.Zero with get, set

    member val Position = Var.create Vector2.Zero with get, set

    member val internal PreviousPosition = Vector2.Zero with get, set

    interface IComponent

type Render () =

    member val R = 0uy with get, set

    member val G = 0uy with get, set

    member val B = 0uy with get, set

    member val Data : Val<Vector2 []> = Val.create [||] with get

    member val Shader : Shader option = None with get, set

    member val Texture : Texture option = None with get, set

    member val DrawKind : DrawKind = DrawKind.LineLoop with get, set

    member val internal VBO = Unchecked.defaultof<Renderer.VBO> with get, set

    member val Position = Val.create Vector2.Zero

    member val internal PreviousPosition = Vector2.Zero with get, set

    member val Rotation = Val.create 0.f

    member val internal PreviousRotation = 0.f with get, set

    interface IComponent   