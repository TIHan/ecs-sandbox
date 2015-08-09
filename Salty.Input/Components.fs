namespace Salty.Input.Components

open Salty.Input

open ECS.Core

type Input () =

    member val MousePosition = Var.create <| MousePosition () with get

    member val Events : Var<InputEvent list> = Var.create [] with get

    interface IComponent<Input>
