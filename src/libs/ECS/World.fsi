namespace ECS.Core

open System

open ECS.Core

[<Sealed>]
type World =

    new : maxEntityCount: int -> World
   
    member InitSystem : ISystem -> unit

    member RunSystem : ISystem -> unit
