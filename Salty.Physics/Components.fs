namespace Salty.Physics.Components

open ECS.Core

open Salty.Core
open Salty.Core.Components

open System.Numerics

type Physics () =

    member val Data : Vector2 [] Var = Var.create [||]

    member val IsStatic = Var.create false

    member val Density = Var.create 0.f

    member val Restitution = Var.create 0.f

    member val Friction = Var.create 0.f

    member val Mass = Var.create 0.f

    member val Body : FarseerPhysics.Dynamics.Body = null with get, set

    member val PolygonShape : FarseerPhysics.Collision.Shapes.PolygonShape = null with get, set

    member val Fixture : FarseerPhysics.Dynamics.Fixture = null with get, set

    interface IComponent<Physics>