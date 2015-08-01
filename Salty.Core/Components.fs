namespace Salty.Core.Components

open System
open System.Numerics

open ECS.Core

type Centroid =
    {
        Var: Var<Vector2>
    }

    interface IComponent

type Position =
    {
        Var: Var<Vector2>
    }

    interface IComponent

type Rotation =
    {
        Var: Var<single>
    }

    interface IComponent
