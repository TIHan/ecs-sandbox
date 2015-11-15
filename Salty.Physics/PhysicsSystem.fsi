namespace Salty.Core.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Core.Physics.Components

open System
open System.Numerics
open System.Xml.Serialization

module Physics =

    val onCollided : World -> IObservable<(Entity * Physics) * (Entity * Physics)>

    val applyImpulse : Vector2 -> Physics -> World -> unit

type PhysicsSystem =

    new : unit -> PhysicsSystem

    interface ISystem