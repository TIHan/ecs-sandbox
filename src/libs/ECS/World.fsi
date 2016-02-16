namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T when 'T :> IECSSystem> =

    member Update : (unit -> unit)

[<Sealed>]
type SystemHandle<'T, 'D1 when 'T :> IECSSystem<'D1>> =

    member Update : ('D1 -> unit)

[<Sealed>]
type World =

    new : maxEntityAmount: int -> World
   
    member AddSystem<'T when 'T :> IECSSystem> : 'T -> SystemHandle<'T>

    member AddSystem<'T, 'D1 when 'T :> IECSSystem<'D1>> : 'T -> SystemHandle<'T, 'D1>
