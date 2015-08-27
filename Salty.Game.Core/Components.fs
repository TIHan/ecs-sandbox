﻿namespace Salty.Game.Core.Components

open ECS.Core

type PlayerCommand =
    | Shoot

type Player () =

    member val IsMovingUp = Var.create false

    member val IsMovingLeft = Var.create false

    member val IsMovingRight = Var.create false

    member val Commands : PlayerCommand ResizeArray = ResizeArray () 

    interface IComponent