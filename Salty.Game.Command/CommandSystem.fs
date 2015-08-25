namespace Salty.Game.Command

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Input
open Salty.Input.Components
open Salty.Game.Core.Components

open System.Collections.Generic

type PlayerCommand =
    | StartMovingUp = 0
    | StopMovingUp = 1
    | StartMovingLeft = 2
    | StopMovingLeft = 3
    | StartMovingRight = 4
    | StopMovingRight = 5

type Command () =

    member val PlayerCommands : Val<PlayerCommand list> = Val.create []

    interface IComponent

type CommandSystem () =
    let inputSystem : ISystem = InputSystem () :> ISystem

    let nextPlayerId = ref 0
    let playerLookup = Dictionary<Input, int> ()

    interface ISystem with

        member __.Init world =
            inputSystem.Init world

            World.componentAdded<Player> world
            |> Observable.add (fun (entity, player) ->
                World.addComponent entity (Command ()) world
                World.addComponent entity (Input ()) world
            )

            // Map input events to commands
            World.componentAdded<Input> world
            |> Observable.add (fun (entity, input) ->
                playerLookup.[input] <- !nextPlayerId

                nextPlayerId := !nextPlayerId + 1

                match world.ComponentQuery.TryGet<Command> entity with
                | Some command ->
                    input.Events
                    |> Observable.map (fun events ->
                        events
                        |> List.choose (function
                            | KeyPressed 'w' when playerLookup.[input] = 0 -> Some PlayerCommand.StartMovingUp
                            | KeyReleased 'w' when playerLookup.[input] = 0 -> Some PlayerCommand.StopMovingUp
                            | KeyPressed 'a' when playerLookup.[input] = 0 -> Some PlayerCommand.StartMovingLeft
                            | KeyReleased 'a' when playerLookup.[input] = 0 -> Some PlayerCommand.StopMovingLeft
                            | KeyPressed 'd' when playerLookup.[input] = 0 -> Some PlayerCommand.StartMovingRight
                            | KeyReleased 'd' when playerLookup.[input] = 0 -> Some PlayerCommand.StopMovingRight
                            | JoystickButtonPressed (id, 10) when id = playerLookup.[input] -> Some PlayerCommand.StartMovingUp
                            | JoystickButtonReleased (id, 10) when id = playerLookup.[input] -> Some PlayerCommand.StopMovingUp
                            | JoystickButtonPressed (id, 2) when id = playerLookup.[input] -> Some PlayerCommand.StartMovingLeft
                            | JoystickButtonReleased (id, 2) when id = playerLookup.[input] -> Some PlayerCommand.StopMovingLeft
                            | JoystickButtonPressed (id, 3) when id = playerLookup.[input] -> Some PlayerCommand.StartMovingRight
                            | JoystickButtonReleased (id, 3) when id = playerLookup.[input] -> Some PlayerCommand.StopMovingRight
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
