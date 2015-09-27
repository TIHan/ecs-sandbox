namespace Salty.Game.Command

open ECS.Core

open Salty.Core
open Salty.Core.Components
open Salty.Core.Input
open Salty.Game.Core.Components

open System.Collections.Generic

type CommandSystem () =

    interface ISystem<Salty> with

        member __.Init world =

            Input.dataUpdated world
            |> Observable.add (fun data ->
                let players = world.ComponentQuery.GetAll<Player> ()

                players
                |> Array.iter (fun (entity, player) ->
                    data.Events
                    |> List.iter (function
                        | KeyPressed 'w'                            -> player.IsMovingUp.Value <- true
                        | KeyReleased 'w'                           -> player.IsMovingUp.Value <- false
                        | KeyPressed 'a'                            -> player.IsMovingLeft.Value <- true
                        | KeyReleased 'a'                           -> player.IsMovingLeft.Value <- false
                        | KeyPressed 'd'                            -> player.IsMovingRight.Value <- true
                        | KeyReleased 'd'                           -> player.IsMovingRight.Value <- false
                        | MouseButtonPressed MouseButtonType.Left   -> player.Commands.Add PlayerCommand.Shoot
                        | JoystickButtonPressed (id, 10)            -> player.IsMovingUp.Value <- true
                        | JoystickButtonReleased (id, 10)           -> player.IsMovingUp.Value <- false
                        | JoystickButtonPressed (id, 2)             -> player.IsMovingLeft.Value <- true
                        | JoystickButtonReleased (id, 2)            -> player.IsMovingLeft.Value <- false
                        | JoystickButtonPressed (id, 3)             -> player.IsMovingRight.Value <- true
                        | JoystickButtonReleased (id, 3)            -> player.IsMovingRight.Value <- false
                        | _ -> ()
                    )
                )
            )

        member __.Update world = ()
