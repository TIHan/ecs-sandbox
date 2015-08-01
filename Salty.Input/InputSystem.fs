namespace Salty.Input

open ECS.Core

type InputEvents = InputEvents of InputEvent list with

    interface IEvent

type InputSystem () =

    interface ISystem with

        member __.Init world =
            ()

        member __.Update world =
            Input.clearEvents ()
            Input.pollEvents ()
            world.EventAggregator.Publish (InputEvents (Input.getEvents ()))