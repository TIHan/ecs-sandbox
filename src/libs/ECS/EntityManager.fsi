namespace BeyondGames.Ecs

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
type IEntityComponent = interface end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Events = 

    /// Published when a component was added to an existing entity.
    [<Sealed>]
    type ComponentAdded<'T when 'T :> IEntityComponent and 'T : not struct> =

        val Entity : Entity

        interface IEntityEvent

    /// Published when a component was removed from an exsting entity.
    [<Sealed>]
    type ComponentRemoved<'T when 'T :> IEntityComponent and 'T : not struct> = 

        val Entity : Entity

        interface IEntityEvent

    /// Published when any component was added to an existing entity.
    [<Sealed>]
    type AnyComponentAdded = 

        val Entity : Entity

        val ComponentType : Type

        interface IEntityEvent

    /// Published when any component was removed from an existing entity.
    [<Sealed>]
    type AnyComponentRemoved =
       
        val Entity : Entity

        val ComponentType : Type

        interface IEntityEvent

    /// Published when an entity has spawned.
    [<Sealed>]
    type EntitySpawned =

        val Entity : Entity

        interface IEntityEvent

    /// Published when an entity was destroyed.
    [<Sealed>]
    type EntityDestroyed =

        val Entity : Entity

        interface IEntityEvent

type ForEachDelegate<'T when 'T :> IEntityComponent and 'T : not struct> = delegate of Entity * byref<'T> -> unit

type ForEachDelegate<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> = Entity -> 'T1 -> 'T2 -> unit//delegate of Entity * byref<'T1> * byref<'T2> -> unit

type ForEachDelegate<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> -> unit

type ForEachDelegate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T4 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> * byref<'T4> -> unit

type TryFindDelegate<'T when 'T :> IEntityComponent and 'T : not struct> = delegate of Entity * byref<'T> -> bool

type TryGetDelegate<'T when 'T :> IEntityComponent and 'T : not struct> = delegate of Entity * byref<'T> -> unit

/// Responsible for querying/adding/removing components and spawning/destroying entities.
[<Sealed>]
type EntityManager =

    static member internal Create : EventManager * maxEntityCount: int -> EntityManager

    // Component Query

    member TryGet<'T when 'T :> IEntityComponent and 'T : not struct> : Entity * TryGetDelegate<'T> -> bool

    member TryFind<'T when 'T :> IEntityComponent and 'T : not struct> : TryFindDelegate<'T> * TryGetDelegate<'T> -> bool

    member ForEach<'T when 'T :> IEntityComponent and 'T : not struct> : ForEachDelegate<'T> -> unit

    member ForEach<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> : ForEachDelegate<'T1, 'T2> -> unit

    member ForEach<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> : ForEachDelegate<'T1, 'T2, 'T3> -> unit

    member ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T4 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> : ForEachDelegate<'T1, 'T2, 'T3, 'T4> -> unit

    member ParallelForEach<'T when 'T :> IEntityComponent and 'T : not struct> : ForEachDelegate<'T> -> unit

    member ParallelForEach<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> : ForEachDelegate<'T1, 'T2> -> unit

    member ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> : ForEachDelegate<'T1, 'T2, 'T3> -> unit

    // Components

    member internal AddComponent<'T when 'T :> IEntityComponent and 'T : not struct> : Entity -> 'T -> unit

    member internal RemoveComponent<'T when 'T :> IEntityComponent and 'T : not struct> : Entity -> unit

    // Entites

    member internal Spawn : (Entity -> unit) -> unit

    member Destroy : Entity -> unit

    member internal Process : unit -> unit

/// Responsible for querying/adding/removing components and spawning/destroying entities.
type Entities = EntityManager

[<Sealed>]
type Aspect<'T when 'T :> IEntityComponent and 'T : not struct> =

    member ForEach : ForEachDelegate<'T> -> unit

[<Sealed>]
type Aspect<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> =

    member ForEach : ForEachDelegate<'T1, 'T2> -> unit

type EntityManager with

    member GetAspect<'T when 'T :> IEntityComponent and 'T : not struct> : unit -> Aspect<'T>

    member GetAspect<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> : unit -> Aspect<'T1, 'T2>