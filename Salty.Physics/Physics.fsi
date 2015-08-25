﻿namespace Salty.Physics

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Physics.Components

open System
open System.Numerics
open System.Xml.Serialization

module World =

    val physicsCollided : IWorld -> IObservable<(Entity * Physics) * (Entity * Physics)>

module Physics =

    val applyForce : Vector2 -> Physics -> unit

type PhysicsSystem =

    new : unit -> PhysicsSystem

    interface ISystem