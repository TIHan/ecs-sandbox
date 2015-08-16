namespace Salty.Game.Core.Components

open ECS.Core

type Player () =

    member val IsMovingUp = Var.create false

    member val IsMovingLeft = Var.create false

    member val IsMovingRight = Var.create false

    interface IComponent<Player>