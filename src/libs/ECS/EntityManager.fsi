namespace ECS

open System
open System.Runtime.InteropServices

#nowarn "9"

/// Unique to the world.
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

/// A marker for component data.
type IECSComponent = interface end

/// Published when a component was added to an existing entity.
[<Sealed>]
type ComponentAdded<'T when 'T :> IECSComponent> =

    val Entity : Entity

    interface IECSEvent

/// Published when a component was removed from an exsting entity.
[<Sealed>]
type ComponentRemoved<'T when 'T :> IECSComponent> = 

    val Entity : Entity

    interface IECSEvent

/// Published when any component was added to an existing entity.
[<Sealed>]
type AnyComponentAdded = 

    val Entity : Entity

    val ComponentType : Type

    interface IECSEvent

/// Published when any component was removed from an existing entity.
[<Sealed>]
type AnyComponentRemoved =
   
    val Entity : Entity

    val ComponentType : Type

    interface IECSEvent

/// Published when an entity has spawned.
[<Sealed>]
type EntitySpawned =

    val Entity : Entity

    interface IECSEvent

/// Published when an entity was destroyed.
[<Sealed>]
type EntityDestroyed =

    val Entity : Entity

    interface IECSEvent

type ForEachDelegate<'T when 'T :> IECSComponent> = delegate of Entity * byref<'T> -> unit

type ForEachDelegate<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> = delegate of Entity * byref<'T1> * byref<'T2> -> unit

type ForEachDelegate<'T1, 'T2, 'T3 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> -> unit

type ForEachDelegate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent and 'T4 :> IECSComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> * byref<'T4> -> unit

/// Responsible for querying/adding/removing components and spawning/destroying entities.
[<Sealed>]
type EntityManager =

    // Component Query

    member TryGet<'T when 'T :> IECSComponent> : Entity -> 'T option

    member TryFind<'T when 'T :> IECSComponent> : (Entity -> 'T -> bool) -> (Entity * 'T) option

    member GetAll<'T when 'T :> IECSComponent> : unit -> (Entity * 'T) []

    member GetAll<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> : unit -> (Entity * 'T1 * 'T2) []

    member ForEach<'T when 'T :> IECSComponent> : ForEachDelegate<'T> -> unit

    member ForEach<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> : ForEachDelegate<'T1, 'T2> -> unit

    member ForEach<'T1, 'T2, 'T3 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent> : ForEachDelegate<'T1, 'T2, 'T3> -> unit

    member ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent and 'T4 :> IECSComponent> : ForEachDelegate<'T1, 'T2, 'T3, 'T4> -> unit

    member ParallelForEach<'T when 'T :> IECSComponent> : ForEachDelegate<'T> -> unit

    member ParallelForEach<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> : ForEachDelegate<'T1, 'T2> -> unit

    member ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent> : ForEachDelegate<'T1, 'T2, 'T3> -> unit

    // Components

    member internal AddComponent<'T when 'T :> IECSComponent> : Entity -> 'T -> unit

    member internal RemoveComponent<'T when 'T :> IECSComponent> : Entity -> unit

    // Entites

    member internal Spawn : (Entity -> unit) -> unit

    member Destroy : Entity -> unit

    member internal Process : unit -> unit

    internal new : EventManager * maxEntityAmount: int -> EntityManager

type Entities = EntityManager