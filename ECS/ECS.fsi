namespace ECS

open System

[<Struct>]
type Entity =
    
    val Id : int

[<Sealed>]
type ComponentObjRef =

    member Value : obj with get

    member IsNull : bool with get

[<Sealed>]
type ComponentRef<'T> =

    member Value : 'T with get

    member IsNull : bool with get

type IEntityQueryContext =

    abstract HasEntityComponent<'T> : Entity -> bool

    abstract TryGetEntityComponent<'T> : Entity -> 'T option

    abstract GetComponentObjRef<'T> : Entity -> ComponentObjRef

    abstract GetComponentRef<'T> : Entity -> ComponentRef<'T>

    abstract IsActive : Entity -> bool

    abstract ForEachActiveEntity : (Entity -> unit) -> unit


    abstract GetActiveEntityComponents<'T> : unit -> (Entity * 'T) []

    abstract GetActiveEntityComponents<'T1, 'T2> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetInactiveEntityComponents<'T> : unit -> (Entity * 'T) []

    abstract GetInactiveEntityComponents<'T1, 'T2> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetEntityComponents<'T> : unit -> (Entity * 'T) []

    abstract GetEntityComponents<'T1, 'T2> : unit -> (Entity * 'T1 * 'T2) []


    abstract ForEachActiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ForEachActiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ForEachActiveEntityComponent<'T1, 'T2, 'T3> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit


    abstract ForEachInactiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ForEachInactiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ForEachEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ForEachEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit

    
    abstract ParallelForEachActiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachActiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachInactiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachInactiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit

[<Sealed>]
type World =

    new : int -> World

    member Time : TimeSpan with get, set

    member Interval : TimeSpan with get, set

    member Delta : single with get, set

    member Run : unit -> unit

    member Query : IEntityQueryContext

    member CreateActiveEntity : int -> (Entity -> unit) -> unit

    member DestroyEntity : Entity -> unit

    member SetEntityComponent<'T> : 'T -> Entity -> unit

    member RemoveEntityComponent<'T> : Entity -> unit

    member AddSystem : ISystem -> unit

    member HandleEvent<'T> : (IObservable<'T> -> unit) -> unit

    member RaiseEvent<'T> : 'T -> unit

and ISystem =

    abstract Init : World -> unit

    abstract Update : World -> unit

