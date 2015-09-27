namespace Salty.Core.Input

open System

open ECS.Core
open Salty.Core

type InputData =
    {
        MousePosition: MousePosition
        Events: InputEvent list
    }

type InputDataUpdated = InputDataUpdated of InputData with

    interface IEventData

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Input =
    
    let dataUpdated : SaltyWorld<IObservable<InputData>> =
        fun world ->
            world.EventAggregator.GetEvent<InputDataUpdated> ()
            |> Observable.map (function
                | InputDataUpdated x -> x
            )

type InputSystem () =

    interface ISystem<Salty> with

        member __.Init _ =
            ()

        member __.Update world =
            Input.clearEvents ()
            Input.pollEvents ()

            let mousePosition = Input.getMousePosition ()
            let events = Input.getEvents ()

            world.EventAggregator.Publish (InputDataUpdated {
                MousePosition = mousePosition
                Events = events
            })
