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
                        | KeyPressed 'w'                            -> __unsafe.setVarValueWithNotify player.IsMovingUp true
                        | KeyReleased 'w'                           -> __unsafe.setVarValueWithNotify player.IsMovingUp false
                        | KeyPressed 'a'                            -> __unsafe.setVarValueWithNotify player.IsMovingLeft true
                        | KeyReleased 'a'                           -> __unsafe.setVarValueWithNotify player.IsMovingLeft false
                        | KeyPressed 'd'                            -> __unsafe.setVarValueWithNotify player.IsMovingRight true
                        | KeyReleased 'd'                           -> __unsafe.setVarValueWithNotify player.IsMovingRight false
                        | MouseButtonPressed MouseButtonType.Left   -> player.Commands.Add PlayerCommand.Shoot
                        | JoystickButtonPressed (id, 10)            -> __unsafe.setVarValueWithNotify player.IsMovingUp true
                        | JoystickButtonReleased (id, 10)           -> __unsafe.setVarValueWithNotify player.IsMovingUp false
                        | JoystickButtonPressed (id, 2)             -> __unsafe.setVarValueWithNotify player.IsMovingLeft true
                        | JoystickButtonReleased (id, 2)            -> __unsafe.setVarValueWithNotify player.IsMovingLeft false
                        | JoystickButtonPressed (id, 3)             -> __unsafe.setVarValueWithNotify player.IsMovingRight true
                        | JoystickButtonReleased (id, 3)            -> __unsafe.setVarValueWithNotify player.IsMovingRight false
                        | _ -> ()
                    )
                )
            )

        member __.Update world = ()
