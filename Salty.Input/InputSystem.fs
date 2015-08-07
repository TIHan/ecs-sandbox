namespace Salty.Input

open Salty.Input.Components

open ECS.Core

type InputSystem = InputSystem of unit with

    interface ISystem with

        member __.Init _ _ _ _ =
            ()

        member __.Update _ _ _ componentQuery =
            Input.clearEvents ()
            Input.pollEvents ()

            let mousePosition = Input.getMousePosition ()
            let events = Input.getEvents ()

            componentQuery.ForEach<Input> (fun (_, input) ->
                input.MousePosition.Value <- mousePosition
                input.Events.Value <- events
            )
