namespace Salty.Game.Core.Components

open ECS.Core
open Salty.Core

type PlayerCommand =
    | Shoot

type Player () =

    member val IsMovingUp = Var.create false

    member val IsMovingLeft = Var.create false

    member val IsMovingRight = Var.create false

    member val Commands : PlayerCommand ResizeArray = ResizeArray ()

    member val IsDead = Val.create false

    interface IComponent