namespace Salty.Game.Command

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Input
open Salty.Input.Components
open Salty.Game.Core.Components

open System.Collections.Generic

type CommandSystem () =
    let inputSystem : ISystem = InputSystem () :> ISystem

    let nextPlayerId = ref 0
    let playerLookup = Dictionary<Input, int> ()

    interface ISystem with

        member __.Init world =
            inputSystem.Init world

            World.componentAdded<Player> world
            |> Observable.add (fun (entity, player) ->
                World.addComponent entity (Input ()) world
            )

            // Process Input on Player
            World.componentAdded<Input> world
            |> Observable.add (fun (entity, input) ->
                playerLookup.[input] <- !nextPlayerId

                nextPlayerId := !nextPlayerId + 1

                match world.ComponentQuery.TryGet<Player> entity with
                | Some player ->
                    input.Events
                    |> Observable.add (fun events ->
                        events
                        |> List.iter (function
                            | KeyPressed 'w' when playerLookup.[input] = 0 -> player.IsMovingUp.Value <- true
                            | KeyReleased 'w' when playerLookup.[input] = 0 -> player.IsMovingUp.Value <- false
                            | KeyPressed 'a' when playerLookup.[input] = 0 -> player.IsMovingLeft.Value <- true
                            | KeyReleased 'a' when playerLookup.[input] = 0 -> player.IsMovingLeft.Value <- false
                            | KeyPressed 'd' when playerLookup.[input] = 0 -> player.IsMovingRight.Value <- true
                            | KeyReleased 'd' when playerLookup.[input] = 0 -> player.IsMovingRight.Value <- false
                            | MouseButtonPressed MouseButtonType.Left when playerLookup.[input] = 0 -> player.Commands.Add PlayerCommand.Shoot
                            | JoystickButtonPressed (id, 10) when id = playerLookup.[input] -> player.IsMovingUp.Value <- true
                            | JoystickButtonReleased (id, 10) when id = playerLookup.[input] -> player.IsMovingUp.Value <- false
                            | JoystickButtonPressed (id, 2) when id = playerLookup.[input] -> player.IsMovingLeft.Value <- true
                            | JoystickButtonReleased (id, 2) when id = playerLookup.[input] -> player.IsMovingLeft.Value <- false
                            | JoystickButtonPressed (id, 3) when id = playerLookup.[input] -> player.IsMovingRight.Value <- true
                            | JoystickButtonReleased (id, 3) when id = playerLookup.[input] -> player.IsMovingRight.Value <- false
                            | _ -> ()
                        )
                    )
                | _ -> ()
            )

        member __.Update world =
            inputSystem.Update world
