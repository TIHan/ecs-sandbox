namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle =

    member Update : (unit -> unit)

[<Sealed>]
type World =

    new : maxEntityAmount: int -> World
   
    member AddSystem : ISystem -> SystemHandle

    member AddSystems : ISystem seq -> SystemHandle