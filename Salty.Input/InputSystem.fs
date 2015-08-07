namespace Salty.Input

open ECS.Core

type InputState =
    {
        MousePosition: MousePosition
        Events: InputEvent list
    }

type InputStateEvent =
    | InputStateUpdated of InputState

    interface IEvent

type InputSystem () =

    interface ISystem with

        member __.Init world =
            ()

        member __.Update world =
            Input.clearEvents ()
            Input.pollEvents ()

            {
                MousePosition = Input.getMousePosition ()
                Events = Input.getEvents ()
            }
            |> InputStateUpdated
            |> world.EventAggregator.Publish