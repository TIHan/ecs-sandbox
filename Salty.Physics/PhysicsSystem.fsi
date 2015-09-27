namespace Salty.Core.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Core.Physics.Components

open System
open System.Numerics
open System.Xml.Serialization

module Physics =

    val collided : SaltyWorld<IObservable<(Entity * Physics) * (Entity * Physics)>>

    val applyImpulse : Vector2 -> Physics -> SaltyWorld<unit>

type PhysicsSystem =

    new : unit -> PhysicsSystem

    interface ISystem<Salty>