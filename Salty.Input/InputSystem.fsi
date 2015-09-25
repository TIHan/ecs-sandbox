namespace Salty.Core.Input

open System

open ECS.Core

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Input =

    val mousePositionUpdated : IWorld -> IObservable<MousePosition>

    val eventsUpdated : IWorld -> IObservable<InputEvent list>

type InputSystem =

    new : unit -> InputSystem

    interface ISystem

