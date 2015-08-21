namespace Salty.Core.Components

open System
open System.Numerics

open ECS.Core

type Centroid () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Position () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent

type SerializationSystem () =

    interface ISystem with

        member this.Init world =
            ()

        member this.Update world =
            ()
