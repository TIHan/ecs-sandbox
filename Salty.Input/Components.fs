namespace Salty.Input.Components

open Salty.Input

open ECS.Core

type Input =
    {
        MousePosition: Var<MousePosition>
        Events: Var<InputEvent list>
    }

    static member Default =
        {
            MousePosition = Var.create <| MousePosition ()
            Events = Var.create []
        }

    interface IComponent
