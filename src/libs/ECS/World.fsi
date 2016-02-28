namespace BeyondGames.Ecs.World

open System
open BeyondGames.Ecs

[<Sealed>]
type SystemHandle<'UpdateData> =

    member Update : 'UpdateData -> unit

    member Dispose : unit -> unit

    interface IDisposable

[<Sealed>]
type World =

    new : maxEntityAmount: int -> World
   
    member AddSystem<'T, 'UpdateData when 'T :> IEntitySystem<'UpdateData>> : 'T -> SystemHandle<'UpdateData>