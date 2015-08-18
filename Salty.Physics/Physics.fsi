namespace Salty.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components

open System.Numerics
open System.Xml.Serialization

module Physics =

    val applyForce : Vector2 -> Entity -> IWorld -> unit

type PhysicsSystem =

    new : unit -> PhysicsSystem

    interface ISystem