namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T when 'T :> ISystem> =

    member Update : (unit -> unit)

[<Sealed>]
type World =

    new : maxEntityAmount: int -> World
   
    member AddSystem<'T when 'T :> ISystem> : 'T -> SystemHandle<'T>
