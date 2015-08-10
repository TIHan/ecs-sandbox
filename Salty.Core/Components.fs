namespace Salty.Core.Components

open System
open System.Numerics

open ECS.Core

type Centroid () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent<Centroid>

type Position () =

    member val Var = Var.create Vector2.Zero with get

    interface IComponent<Position>

type Rotation () =

    member val Var = Var.create 0.f with get

    interface IComponent<Rotation>

type SerializationSystem () =

    interface ISystem with

        member this.Init world =
            ()

        member this.Update world =
            ()
