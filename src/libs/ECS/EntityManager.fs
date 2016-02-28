namespace BeyondGames.Ecs

open System
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices

#nowarn "9"

/// This is internal use only.
module DataStructures =

    [<ReferenceEquality>]
    type UnsafeResizeArray<'T> =
        {
            mutable count: int
            mutable buffer: 'T []
        }

        static member Create capacity =
            if capacity <= 0 then
                failwith "Capacity must be greater than 0"

            {
                count = 0
                buffer = Array.zeroCreate<'T> capacity
            }

        member this.IncreaseCapacity () =
            let newLength = uint32 this.buffer.Length * 2u
            if newLength >= uint32 Int32.MaxValue then
                failwith "Length is bigger than the maximum number of elements in the array"

            let newBuffer = Array.zeroCreate<'T> (int newLength)
            Array.Copy (this.buffer, newBuffer, this.count)
            this.buffer <- newBuffer
             

        member inline this.Add item =
            if this.count >= this.buffer.Length then
                this.IncreaseCapacity ()
            
            this.buffer.[this.count] <- item
            this.count <- this.count + 1

        member inline this.LastItem = this.buffer.[this.count - 1]

        member inline this.SwapRemoveAt index =
            if index >= this.count then
                failwith "Index out of bounds"

            let lastIndex = this.count - 1

            this.buffer.[index] <- this.buffer.[lastIndex]
            this.buffer.[lastIndex] <- Unchecked.defaultof<'T>
            this.count <- lastIndex

        member inline this.Count = this.count
        member inline this.Buffer = this.buffer

open DataStructures

[<Struct; StructLayout (LayoutKind.Explicit)>]
type Entity =

    [<FieldOffset (0)>]
    val Index : int

    [<FieldOffset (4)>]
    val Version : uint32

    [<FieldOffset (0); DefaultValue>]
    val Id : uint64

    new (index, version) = { Index = index; Version = version }

    override this.ToString () = String.Format ("(Entity #{0})", this.Id)

type IEntityComponent = interface end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Events =

    [<Sealed>]
    type ComponentAdded<'T when 'T :> IEntityComponent and 'T : not struct> =

        val Entity : Entity

        new (entity) = { Entity = entity }

        interface IEntityEvent

    [<Sealed>]
    type ComponentRemoved<'T when 'T :> IEntityComponent and 'T : not struct> =

        val Entity : Entity

        new (entity) = { Entity = entity }

        interface IEntityEvent

    [<Sealed>]
    type AnyComponentAdded =

        val Entity : Entity

        val ComponentType : Type

        new (entity, typ) = { Entity = entity; ComponentType = typ }

        interface IEntityEvent

    [<Sealed>]
    type AnyComponentRemoved =

        val Entity : Entity

        val ComponentType : Type

        new (entity, typ) = { Entity = entity; ComponentType = typ }

        interface IEntityEvent

    [<Sealed>]
    type EntitySpawned =

        val Entity : Entity

        new (entity) = { Entity = entity }

        interface IEntityEvent

    [<Sealed>]
    type EntityDestroyed =

        val Entity : Entity

        new (entity) = { Entity = entity }

        interface IEntityEvent

open Events

type IEntityLookupData =

    abstract Entities : Entity UnsafeResizeArray with get

type ForEachDelegate<'T when 'T :> IEntityComponent and 'T : not struct> = delegate of Entity * byref<'T> -> unit

type ForEachDelegate<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> = Entity -> 'T1 -> 'T2 -> unit//delegate of Entity * byref<'T1> * byref<'T2> -> unit

type ForEachDelegate<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> -> unit

type ForEachDelegate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T4 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> * byref<'T4> -> unit

type TryFindDelegate<'T when 'T :> IEntityComponent and 'T : not struct> = delegate of Entity * byref<'T> -> bool

type TryGetDelegate<'T when 'T :> IEntityComponent and 'T : not struct> = delegate of Entity * byref<'T> -> unit

[<ReferenceEquality>]
type EntityLookupData<'T when 'T :> IEntityComponent and 'T : not struct> =
    {
        ComponentAddedEvent: Event<ComponentAdded<'T>>
        ComponentRemovedEvent: Event<ComponentRemoved<'T>>

        RemoveComponent: Entity -> unit
        RemoveComponentNow: Entity -> unit

        Active: bool []
        IndexLookup: int []
        Entities: Entity UnsafeResizeArray
        Components: 'T UnsafeResizeArray
    }

    interface IEntityLookupData with

        member this.Entities = this.Entities

module AspectIterations =

    let inline iter<'T when 'T :> IEntityComponent and 'T : not struct> (del: ForEachDelegate<'T>) useParallelism (data: EntityLookupData<'T>) (activeIndices: bool []) =
        let count = data.Entities.Count
        let active = data.Active
        let entities = data.Entities.Buffer
        let components = data.Components.Buffer

        let inline iter i = 
            let entity = entities.[i]

            if active.[entity.Index] && activeIndices.[entity.Index] then
                del.Invoke (entity, &components.[i])

        if useParallelism
        then Parallel.For (0, count, iter) |> ignore
        else
            for i = 0 to count - 1 do
                iter i

    let inline iter2<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> (del: ForEachDelegate<'T1, 'T2>) useParallelism (data: IEntityLookupData) (data1: EntityLookupData<'T1>) (data2: EntityLookupData<'T2>) (activeIndices: bool []) : unit =
        let count = data.Entities.Count
        let entities = data.Entities.Buffer

        let inline iter i =
            let entity = entities.[i]

            if activeIndices.[entity.Index] then
                let comp1Index = data1.IndexLookup.[entity.Index]
                let comp2Index = data2.IndexLookup.[entity.Index]

                if comp1Index >= 0 && comp2Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] then
                    del entity data1.Components.Buffer.[comp1Index] data2.Components.Buffer.[comp2Index]

        if useParallelism
        then Parallel.For (0, count, iter) |> ignore
        else
            for i = 0 to count - 1 do
                iter i

    let inline iter3<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> (del: ForEachDelegate<'T1, 'T2, 'T3>) useParallelism (data: IEntityLookupData) (data1: EntityLookupData<'T1>) (data2: EntityLookupData<'T2>) (data3: EntityLookupData<'T3>) (activeIndices: bool []) : unit =
        let count = data.Entities.Count
        let entities = data.Entities.Buffer

        let inline iter i =
            let entity = entities.[i]

            if activeIndices.[entity.Index] then
                let comp1Index = data1.IndexLookup.[entity.Index]
                let comp2Index = data2.IndexLookup.[entity.Index]
                let comp3Index = data3.IndexLookup.[entity.Index]

                if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] && data3.Active.[entity.Index] then
                    del.Invoke (entity, &data1.Components.Buffer.[comp1Index], &data2.Components.Buffer.[comp2Index], &data3.Components.Buffer.[comp3Index])

        if useParallelism
        then Parallel.For (0, count, iter) |> ignore
        else
            for i = 0 to count - 1 do
                iter i

    let inline iter4<'T1, 'T2, 'T3, 'T4 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T4 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> (del: ForEachDelegate<'T1, 'T2, 'T3, 'T4>) useParallelism (data: IEntityLookupData) (data1: EntityLookupData<'T1>) (data2: EntityLookupData<'T2>) (data3: EntityLookupData<'T3>) (data4: EntityLookupData<'T4>) (activeIndices: bool []) : unit =
        let count = data.Entities.Count
        let entities = data.Entities.Buffer

        let inline iter i =
            let entity = entities.[i]

            if activeIndices.[entity.Index] then
                let comp1Index = data1.IndexLookup.[entity.Index]
                let comp2Index = data2.IndexLookup.[entity.Index]
                let comp3Index = data3.IndexLookup.[entity.Index]
                let comp4Index = data4.IndexLookup.[entity.Index]

                if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && comp4Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] && data3.Active.[entity.Index] && data4.Active.[entity.Index] then
                    del.Invoke (entity, &data1.Components.Buffer.[comp1Index], &data2.Components.Buffer.[comp2Index], &data3.Components.Buffer.[comp3Index], &data4.Components.Buffer.[comp4Index])

        if useParallelism
        then Parallel.For (0, count, iter) |> ignore
        else
            for i = 0 to count - 1 do
                iter i

[<ReferenceEquality>]
type EntityManager =
    {
        EventManager: EventManager

        MaxEntityAmount: int
        Lookup: ConcurrentDictionary<Type, IEntityLookupData>

        ActiveVersions: uint32 []
        ActiveIndices: bool []

        mutable nextEntityIndex: int
        RemovedEntityQueue: Queue<Entity>

        EntityRemovals: ((Entity -> unit) ResizeArray) []

        AddComponentQueue: ConcurrentQueue<unit -> unit>
        RemoveComponentQueue: ConcurrentQueue<unit -> unit>

        SpawnEntityQueue: ConcurrentQueue<unit -> unit>
        DestroyEntityQueue: ConcurrentQueue<Entity>

        EmitAddComponentEventQueue: Queue<unit -> unit>
        EmitRemoveComponentEventQueue: Queue<unit -> unit>

        EmitSpawnEntityEventQueue: Queue<unit -> unit>
        EmitDestroyEntityEventQueue: Queue<unit -> unit>

        EntitySpawnedEvent: Event<EntitySpawned>
        EntityDestroyedEvent: Event<EntityDestroyed>

        AnyComponentAddedEvent: Event<AnyComponentAdded>
        AnyComponentRemovedEvent: Event<AnyComponentRemoved>
    }

    static member Create (eventManager: EventManager, maxEntityAmount) =
        if maxEntityAmount <= 0 then
            failwith "Max entity amount must be greater than 0."

        let maxEntityAmount = maxEntityAmount + 1
        let lookup = ConcurrentDictionary<Type, IEntityLookupData> ()

        let activeVersions = Array.init maxEntityAmount (fun _ -> 0u)
        let activeIndices = Array.zeroCreate<bool> maxEntityAmount

        let mutable nextEntityIndex = 1
        let removedEntityQueue = Queue<Entity> () 

        let entityRemovals : ((Entity -> unit) ResizeArray) [] = Array.init maxEntityAmount (fun _ -> ResizeArray ())

        let addComponentQueue = ConcurrentQueue<unit -> unit> ()
        let removeComponentQueue = ConcurrentQueue<unit -> unit> ()

        let spawnEntityQueue = ConcurrentQueue<unit -> unit> ()
        let destroyEntityQueue = ConcurrentQueue<Entity> ()

        let emitAddComponentEventQueue = Queue<unit -> unit> ()
        let emitRemoveComponentEventQueue = Queue<unit -> unit> ()

        let emitSpawnEntityEventQueue = Queue<unit -> unit> ()
        let emitDestroyEntityEventQueue = Queue<unit -> unit> ()

        let entitySpawnedEvent = eventManager.GetEvent<EntitySpawned> ()
        let entityDestroyedEvent = eventManager.GetEvent<EntityDestroyed> ()

        let anyComponentAddedEvent = eventManager.GetEvent<AnyComponentAdded> ()
        let anyComponentRemovedEvent = eventManager.GetEvent<AnyComponentRemoved> ()

        {
            EventManager = eventManager
            MaxEntityAmount = maxEntityAmount
            Lookup = lookup
            ActiveVersions = activeVersions
            ActiveIndices = activeIndices
            nextEntityIndex = nextEntityIndex
            RemovedEntityQueue = removedEntityQueue
            EntityRemovals = entityRemovals
            AddComponentQueue = addComponentQueue
            RemoveComponentQueue = removeComponentQueue
            SpawnEntityQueue = spawnEntityQueue
            DestroyEntityQueue = destroyEntityQueue
            EmitAddComponentEventQueue = emitAddComponentEventQueue
            EmitRemoveComponentEventQueue = emitRemoveComponentEventQueue
            EmitSpawnEntityEventQueue = emitSpawnEntityEventQueue
            EmitDestroyEntityEventQueue = emitDestroyEntityEventQueue
            EntitySpawnedEvent = entitySpawnedEvent
            EntityDestroyedEvent = entityDestroyedEvent
            AnyComponentAddedEvent = anyComponentAddedEvent
            AnyComponentRemovedEvent = anyComponentRemovedEvent
        }

    member inline this.ProcessQueue (queue: Queue<unit -> unit>) =
        while queue.Count > 0 do
            queue.Dequeue () ()

    member inline this.ProcessConcurrentQueue (queue: ConcurrentQueue<unit -> unit>) =
        let mutable f = Unchecked.defaultof<unit -> unit>
        while queue.TryDequeue (&f) do
            f ()

    member inline this.IsValidEntity (entity: Entity) =
        not (entity.Index.Equals 0u) && this.ActiveVersions.[entity.Index].Equals entity.Version

    member this.Process () =
        while
            not this.AddComponentQueue.IsEmpty          ||
            not this.RemoveComponentQueue.IsEmpty       ||
            not this.SpawnEntityQueue.IsEmpty           ||
            not this.DestroyEntityQueue.IsEmpty
                do

            // ******************************************
            // ************ Entity and Component Removing
            // ******************************************
            let mutable entity = Unchecked.defaultof<Entity>
            while this.DestroyEntityQueue.TryDequeue (&entity) do
                this.DestoryNow entity

            this.ProcessConcurrentQueue  this.RemoveComponentQueue

            this.ProcessQueue            this.EmitRemoveComponentEventQueue
            this.ProcessQueue            this.EmitDestroyEntityEventQueue
            // ******************************************

            // ******************************************
            // ************** Entity and Component Adding
            // ******************************************
            this.ProcessConcurrentQueue  this.SpawnEntityQueue
            this.ProcessConcurrentQueue  this.AddComponentQueue

            this.ProcessQueue            this.EmitAddComponentEventQueue
            this.ProcessQueue            this.EmitSpawnEntityEventQueue
            // ******************************************

    member this.GetEntityLookupData<'T when 'T :> IEntityComponent and 'T : not struct> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        match this.Lookup.TryGetValue(t, &data) with
        | true -> data :?> EntityLookupData<'T>
        | _ ->
            let factory t =
                let data =
                    {
                        ComponentAddedEvent = this.EventManager.GetEvent<ComponentAdded<'T>> ()
                        ComponentRemovedEvent = this.EventManager.GetEvent<ComponentRemoved<'T>> ()

                        RemoveComponent = fun entity -> this.RemoveComponent<'T> entity
                        RemoveComponentNow = fun entity -> this.RemoveComponentNow<'T> entity

                        Active = Array.zeroCreate<bool> this.MaxEntityAmount
                        IndexLookup = Array.init this.MaxEntityAmount (fun _ -> -1) // -1 means that no component exists for that entity
                        Entities = UnsafeResizeArray.Create 1
                        Components = UnsafeResizeArray.Create 1
                    }

                data :> IEntityLookupData

            this.Lookup.GetOrAdd(t, factory) :?> EntityLookupData<'T>

    member inline this.Iterate<'T when 'T :> IEntityComponent and 'T : not struct> (del: ForEachDelegate<'T>, useParallelism: bool) : unit =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            AspectIterations.iter del useParallelism data this.ActiveIndices

    member inline this.Iterate<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> (del: ForEachDelegate<'T1, 'T2>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) then
            let data = [|data1;data2|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            AspectIterations.iter2 del useParallelism data data1 data2 this.ActiveIndices

    member inline this.Iterate<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> (del: ForEachDelegate<'T1, 'T2, 'T3>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) && 
           this.Lookup.TryGetValue (typeof<'T3>, &data3) then
            let data = [|data1;data2;data3|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>
            AspectIterations.iter3 del useParallelism data data1 data2 data3 this.ActiveIndices

    member inline this.Iterate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T4 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> (del: ForEachDelegate<'T1, 'T2, 'T3, 'T4>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        let mutable data4 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) && 
           this.Lookup.TryGetValue (typeof<'T3>, &data3) && this.Lookup.TryGetValue (typeof<'T4>, &data4) then
            let data = [|data1;data2;data3;data4|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>
            let data4 = data4 :?> EntityLookupData<'T4>
            AspectIterations.iter4 del useParallelism data data1 data2 data3 data4 this.ActiveIndices

    // Components

    member this.AddComponent<'T when 'T :> IEntityComponent and 'T : not struct> (entity: Entity) (comp: 'T) =
        this.AddComponentQueue.Enqueue (fun () ->

            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    printfn "ECS WARNING: Component, %s, already added to %A." typeof<'T>.Name entity
                else
                    this.EntityRemovals.[entity.Index].Add (data.RemoveComponentNow)

                    data.Active.[entity.Index] <- true
                    data.IndexLookup.[entity.Index] <- data.Entities.Count

                    data.Components.Add comp
                    data.Entities.Add entity

                    this.EmitAddComponentEventQueue.Enqueue (fun () ->
                        this.AnyComponentAddedEvent.Trigger (AnyComponentAdded (entity, typeof<'T>))
                        data.ComponentAddedEvent.Trigger (ComponentAdded<'T> (entity))
                    )

            else
                printfn "ECS WARNING: %A is invalid. Cannot add component, %s." entity typeof<'T>.Name

        )

    member this.RemoveComponentNow<'T when 'T :> IEntityComponent and 'T : not struct> (entity: Entity) =
        if this.IsValidEntity entity then
            let data = this.GetEntityLookupData<'T> ()

            if data.IndexLookup.[entity.Index] >= 0 then
                let index = data.IndexLookup.[entity.Index]
                let swappingEntity = data.Entities.LastItem

                data.Entities.SwapRemoveAt index
                data.Components.SwapRemoveAt index

                data.Active.[entity.Index] <- false
                data.IndexLookup.[entity.Index] <- -1

                if not (entity.Index.Equals swappingEntity.Index) then
                    data.IndexLookup.[swappingEntity.Index] <- index

                this.EmitRemoveComponentEventQueue.Enqueue (fun () ->
                    this.AnyComponentRemovedEvent.Trigger (AnyComponentRemoved (entity, typeof<'T>))
                    data.ComponentRemovedEvent.Trigger (ComponentRemoved<'T> (entity))
                )
            else
                printfn "ECS WARNING: Component, %s, does not exist on %A." typeof<'T>.Name entity

        else
            printfn "ECS WARNING: %A is invalid. Cannot remove component, %s." entity typeof<'T>.Name

    member this.RemoveComponent<'T when 'T :> IEntityComponent and 'T : not struct> (entity: Entity) =
        this.RemoveComponentQueue.Enqueue (fun () -> this.RemoveComponentNow<'T> (entity))

    // Entities

    member this.Spawn f =             
        this.SpawnEntityQueue.Enqueue (fun () ->

            if this.RemovedEntityQueue.Count = 0 && this.nextEntityIndex >= this.MaxEntityAmount then
                printfn "ECS WARNING: Unable to spawn entity. Max entity amount hit: %i." (this.MaxEntityAmount - 1)
            else
                let entity =
                    if this.RemovedEntityQueue.Count > 0 then
                        let entity = this.RemovedEntityQueue.Dequeue ()
                        Entity (entity.Index, entity.Version + 1u)
                    else
                        let index = this.nextEntityIndex
                        this.nextEntityIndex <- index + 1
                        Entity (index, 0u)

                this.ActiveVersions.[entity.Index] <- entity.Version
                this.ActiveIndices.[entity.Index] <- true

                this.EmitSpawnEntityEventQueue.Enqueue (fun () ->
                    this.EntitySpawnedEvent.Trigger (EntitySpawned (entity))
                )

                f entity

        )
       
    member this.DestoryNow (entity: Entity) =
        if this.IsValidEntity entity then
            let removals = this.EntityRemovals.[entity.Index]
            removals.ForEach (fun f -> f entity)
            removals.Clear ()
            this.RemovedEntityQueue.Enqueue entity  

            this.ActiveVersions.[entity.Index] <- 0u
            this.ActiveIndices.[entity.Index] <- false

            this.EmitDestroyEntityEventQueue.Enqueue (fun () ->
                this.EntityDestroyedEvent.Trigger (EntityDestroyed (entity))
            )
        else
            printfn "ECS WARNING: %A is invalid. Cannot destroy." entity

    member this.Destroy (entity: Entity) =
        this.DestroyEntityQueue.Enqueue (entity)  

    // Component Query

    member this.TryGet<'T when 'T :> IEntityComponent and 'T : not struct> (entity: Entity, result: TryGetDelegate<'T>) : bool =
        if this.IsValidEntity entity then
            let mutable data = Unchecked.defaultof<IEntityLookupData>
            if this.Lookup.TryGetValue (typeof<'T>, &data) then
                let data = data :?> EntityLookupData<'T>

                let index = data.IndexLookup.[entity.Index]
                if index >= 0 then
                    result.Invoke (entity, &data.Components.Buffer.[index])
                    true
                else
                    false
            else
                false
        else
            false


    member this.TryFind<'T when 'T :> IEntityComponent and 'T : not struct> (predicate: TryFindDelegate<'T>, result: TryGetDelegate<'T>) : bool =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let count = data.Entities.Count

            let rec tryFind isFound = function
                | i when i >= count -> isFound
                | i ->
                    let entity = data.Entities.Buffer.[i]

                    if this.ActiveIndices.[entity.Index] && data.Active.[entity.Index] && predicate.Invoke (entity, &data.Components.Buffer.[i]) then 
                        result.Invoke (entity, &data.Components.Buffer.[i])
                        tryFind true count
                    else
                        tryFind false (i + 1)

            tryFind false 0
        else
            false

    member this.ForEach<'T when 'T :> IEntityComponent and 'T : not struct> f : unit =
        this.Iterate<'T> (f, false)

    member this.ForEach<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> f : unit =
        this.Iterate<'T1, 'T2> (f, false)

    member this.ForEach<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> f : unit =
        this.Iterate<'T1, 'T2, 'T3> (f, false)

    member this.ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T4 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct and 'T4 : not struct> f : unit =
        this.Iterate<'T1, 'T2, 'T3, 'T4> (f, false)

    member this.ParallelForEach<'T when 'T :> IEntityComponent and 'T : not struct> f : unit =
        this.Iterate<'T> (f, true)

    member this.ParallelForEach<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> f : unit =
        this.Iterate<'T1, 'T2> (f, true)

    member this.ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T3 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct and 'T3 : not struct> f : unit =
        this.Iterate<'T1, 'T2, 'T3> (f, true)

type Entities = EntityManager

[<ReferenceEquality>]
type Aspect<'T when 'T :> IEntityComponent and 'T : not struct> =
    {
        Data: EntityLookupData<'T>
        EntityManager: EntityManager
    }

    member this.ForEach (f) =
        AspectIterations.iter f false this.Data this.EntityManager.ActiveIndices

[<ReferenceEquality>]
type Aspect<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> =
    {
        Data: IEntityLookupData
        Data1: EntityLookupData<'T1>
        Data2: EntityLookupData<'T2>
        EntityManager: EntityManager
    }

    member this.ForEach (f) =
        AspectIterations.iter2 f false this.Data this.Data1 this.Data2 this.EntityManager.ActiveIndices

type EntityManager with

    member this.GetAspect<'T when 'T :> IEntityComponent and 'T : not struct> () : Aspect<'T> =
        let data = this.GetEntityLookupData<'T> ()
        {
            Data = data
            EntityManager = this
        }

    member this.GetAspect<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> () =
        let data1 = this.GetEntityLookupData<'T1> ()
        let data2 = this.GetEntityLookupData<'T2> ()
        let data = [|data1 :> IEntityLookupData; data2 :> IEntityLookupData|] |> Array.minBy (fun x -> x.Entities.Count)
        {
            Data = data
            Data1 = data1
            Data2 = data2
            EntityManager = this
        }