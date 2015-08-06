namespace Salty.Input

open ECS.Core

type InputSystem () =

    interface ISystem with

        member __.Init world =
            ()

        member __.Update world =
            Input.clearEvents ()
            Input.pollEvents ()
            Input.getEvents ()
            |> List.iter world.EventAggregator.Publish