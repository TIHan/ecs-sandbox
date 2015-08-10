namespace Salty.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components

open System.Numerics
open System.Xml.Serialization

module Components =

    type Physics =

        new : unit -> Physics

        member Data : Var<Vector2 []>

        member IsStatic : Var<bool>

        member Density : Var<single>

        member Restitution : Var<single>

        member Friction : Var<single>

        member Mass : Var<single>

        member Position : Val<Vector2>

        member Rotation : Val<single>

        interface IComponent<Physics>

        interface IXmlSerializable

type PhysicsSystem =

    new : unit -> PhysicsSystem

    interface ISystem