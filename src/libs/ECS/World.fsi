namespace ECS.World

open ECS

[<Sealed>]
type SystemHandle<'T, 'UpdateData when 'T :> IECSSystem<'UpdateData>> =

    member Update : ('UpdateData -> unit)

[<Sealed>]
type World =

    new : maxEntityAmount: int -> World
   
    member AddSystem<'T, 'UpdateData when 'T :> IECSSystem<'UpdateData>> : 'T -> SystemHandle<'T, 'UpdateData>
