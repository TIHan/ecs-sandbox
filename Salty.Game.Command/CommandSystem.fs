namespace Salty.Game.Command

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Input
open Salty.Input.Components
open Salty.Game.Core.Components

type PlayerCommand =
    | StartMovingUp = 0
    | StopMovingUp = 1
    | StartMovingLeft = 2
    | StopMovingLeft = 3
    | StartMovingRight = 4
    | StopMovingRight = 5

type Command () =

    member val PlayerCommands : Val<PlayerCommand list> = Val.create []

    interface IComponent<Command>

type CommandSystem () =
    let inputSystem : ISystem = InputSystem () :> ISystem

    interface ISystem with

        member __.Init world =
            inputSystem.Init world

            World.componentAdded<Player> world
            |> Observable.add (fun (entity, player) ->
                World.Entity.addComponent entity (Command ()) world
                World.Entity.addComponent entity (Input ()) world
            )

            // Map input events to commands
            World.componentAdded<Input> world
            |> Observable.add (fun (entity, input) ->
                match world.ComponentQuery.TryGet<Command> entity with
                | Some command ->
                    input.Events
                    |> Observable.map (fun events ->
                        events
                        |> List.choose (function
                            | KeyPressed 'w' -> Some PlayerCommand.StartMovingUp
                            | KeyReleased 'w' -> Some PlayerCommand.StopMovingUp
                            | KeyPressed 'a' -> Some PlayerCommand.StartMovingLeft
                            | KeyReleased 'a' -> Some PlayerCommand.StopMovingLeft
                            | KeyPressed 'd' -> Some PlayerCommand.StartMovingRight
                            | KeyReleased 'd' -> Some PlayerCommand.StopMovingRight
                            | _ -> None
                        )
                    )
                    |> command.PlayerCommands.Assign
                | _ -> ()
            )

            // Process commands on players
            World.componentAdded<Command> world
            |> Observable.add (fun (entity, command) ->
                match world.ComponentQuery.TryGet<Player> entity with
                | Some player ->
                    command.PlayerCommands
                    |> Observable.add (fun commands ->
                        commands
                        |> List.iter (function
                            | PlayerCommand.StartMovingUp -> player.IsMovingUp.Value <- true
                            | PlayerCommand.StopMovingUp -> player.IsMovingUp.Value <- false
                            | PlayerCommand.StartMovingLeft -> player.IsMovingLeft.Value <- true
                            | PlayerCommand.StopMovingLeft -> player.IsMovingLeft.Value <- false
                            | PlayerCommand.StartMovingRight -> player.IsMovingRight.Value <- true
                            | PlayerCommand.StopMovingRight -> player.IsMovingRight.Value <- false
                            | _ -> ()
                        )
                    )
                | _ -> ()
            )

        member __.Update world =
            inputSystem.Update world
