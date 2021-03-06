﻿namespace Salty.Core.Input

open System

open ECS.Core
open Salty.Core

type InputData =
    {
        MousePosition: MousePosition
        Events: InputEvent list
    }

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Input =

    val dataUpdated : SaltyWorld<IObservable<InputData>>

type InputSystem =

    new : unit -> InputSystem

    interface ISystem<Salty>

