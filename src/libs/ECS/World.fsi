namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T when 'T :> IECSSystem> =

    member Update : (unit -> unit)

[<Sealed>]
type World =

    new : maxEntityAmount: int -> World
   
    member AddSystem<'T when 'T :> IECSSystem> : 'T -> SystemHandle<'T>
