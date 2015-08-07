namespace Salty.Input

open Salty.Input.Components

open ECS.Core

type InputSystem () =

    interface ISystem with

        member __.Init world =
            ()

        member __.Update world =
            Input.clearEvents ()
            Input.pollEvents ()

            let mousePosition = Input.getMousePosition ()
            let events = Input.getEvents ()

            world.EntityQuery.ForEachActiveComponent<Input> (fun (_, input) ->
                input.MousePosition.Value <- mousePosition
                input.Events.Value <- events
            )
