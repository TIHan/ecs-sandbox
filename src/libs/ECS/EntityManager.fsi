namespace ECS

open System
open System.Runtime.InteropServices

#nowarn "9"

[<Struct; StructLayout (LayoutKind.Explicit)>]
type Entity =

    [<FieldOffset (0)>]
    val Index : int

    /// Version of the Entity in relation to its index. 
    /// Re-using the index, will increment the version by one. Doing this repeatly, for example, 60 times a second, it will take more than two years to overflow.
    [<FieldOffset (4)>]
    val Version : uint32

    [<FieldOffset (0); DefaultValue>]
    val Id : uint64

    new : int * uint32 -> Entity

type IComponent = interface end

[<Sealed>]
type ComponentAdded<'T when 'T :> IComponent> =

    val Entity : Entity

    interface IEvent

[<Sealed>]
type ComponentRemoved<'T when 'T :> IComponent> = 

    val Entity : Entity

    interface IEvent

[<Sealed>]
type AnyComponentAdded = 

    val Entity : Entity

    val ComponentType : Type

    interface IEvent

[<Sealed>]
type AnyComponentRemoved =
   
    val Entity : Entity

    val ComponentType : Type

    interface IEvent

[<Sealed>]
type EntitySpawned =

    val Entity : Entity

    interface IEvent

[<Sealed>]
type EntityDestroyed =

    val Entity : Entity

    interface IEvent

type ForEachDelegate<'T when 'T :> IComponent> = delegate of Entity * byref<'T> -> unit

type ForEachDelegate<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> = delegate of Entity * byref<'T1> * byref<'T2> -> unit

type ForEachDelegate<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> -> unit

type ForEachDelegate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> * byref<'T4> -> unit

[<Sealed>]
type EntityManager =

    // Component Query

    member TryGet<'T when 'T :> IComponent> : Entity -> 'T option

    member TryFind<'T when 'T :> IComponent> : (Entity -> 'T -> bool) -> (Entity * 'T) option

    member GetAll<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    member GetAll<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []

    member ForEach<'T when 'T :> IComponent> : ForEachDelegate<'T> -> unit

    member ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : ForEachDelegate<'T1, 'T2> -> unit

    member ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : ForEachDelegate<'T1, 'T2, 'T3> -> unit

    member ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> : ForEachDelegate<'T1, 'T2, 'T3, 'T4> -> unit

    member ParallelForEach<'T when 'T :> IComponent> : ForEachDelegate<'T> -> unit

    member ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : ForEachDelegate<'T1, 'T2> -> unit

    member ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : ForEachDelegate<'T1, 'T2, 'T3> -> unit

    // Components

    member internal AddComponent<'T when 'T :> IComponent> : Entity -> 'T -> unit

    member internal RemoveComponent<'T when 'T :> IComponent> : Entity -> unit

    // Entites

    member internal Spawn : (Entity -> unit) -> unit

    member Destroy : Entity -> unit

    member internal Process : unit -> unit

    internal new : EventAggregator * maxEntityAmount: int -> EntityManager