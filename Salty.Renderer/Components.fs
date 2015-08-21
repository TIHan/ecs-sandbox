namespace Salty.Renderer.Components

open ECS.Core

open System.Numerics

type Camera () =

    member val Projection = Matrix4x4.Identity with get, set

    member val PreviousProjection = Val.create Matrix4x4.Identity with get, set

    member val View = Matrix4x4.Identity with get, set

    member val ViewportPosition = Vector2.Zero with get, set

    member val ViewportDimensions = Vector2.Zero with get, set

    member val ViewportDepth = Vector2.Zero with get, set

    member val Position = Var.create Vector2.Zero with get, set

    member val PreviousPosition = Val.create Vector2.Zero with get, set

    interface IComponent

type Render () =

    member val R = 0uy with get, set

    member val G = 0uy with get, set

    member val B = 0uy with get, set

    member val VBO = Unchecked.defaultof<Renderer.VBO> with get, set

    member val Position = Val.create Vector2.Zero

    member val PreviousPosition = Val.create Vector2.Zero

    member val Rotation = Val.create 0.f

    member val PreviousRotation = Val.create 0.f

    interface IComponent   