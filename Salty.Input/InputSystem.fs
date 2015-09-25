namespace Salty.Core.Input

open System

open ECS.Core

type MousePositionUpdated = MousePositionUpdated of MousePosition with

    interface IEventData

type InputEventsUpdated = InputEventsUpdated of InputEvent list with

    interface IEventData

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Input =
    
    let mousePositionUpdated (world: IWorld) =
        world.EventAggregator.GetEvent<MousePositionUpdated> ()
        |> Observable.map (fun (MousePositionUpdated x) -> x)

    let eventsUpdated (world: IWorld) =
        world.EventAggregator.GetEvent<InputEventsUpdated> ()
        |> Observable.map (fun (InputEventsUpdated x) -> x)

type InputSystem () =

    interface ISystem with

        member __.Init _ =
            ()

        member __.Update world =
            Input.clearEvents ()
            Input.pollEvents ()

            let mousePosition = Input.getMousePosition ()
            let events = Input.getEvents ()

            world.EventAggregator.Publish (MousePositionUpdated mousePosition)
            world.EventAggregator.Publish (InputEventsUpdated events)
