namespace ECS

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

type IECSComponent = interface end

[<Sealed>]
type ComponentAdded<'T when 'T :> IECSComponent> =

    val Entity : Entity

    new (entity) = { Entity = entity }

    interface IECSEvent

[<Sealed>]
type ComponentRemoved<'T when 'T :> IECSComponent> =

    val Entity : Entity

    new (entity) = { Entity = entity }

    interface IECSEvent

[<Sealed>]
type AnyComponentAdded =

    val Entity : Entity

    val ComponentType : Type

    new (entity, typ) = { Entity = entity; ComponentType = typ }

    interface IECSEvent

[<Sealed>]
type AnyComponentRemoved =

    val Entity : Entity

    val ComponentType : Type

    new (entity, typ) = { Entity = entity; ComponentType = typ }

    interface IECSEvent

[<Sealed>]
type EntitySpawned =

    val Entity : Entity

    new (entity) = { Entity = entity }

    interface IECSEvent

[<Sealed>]
type EntityDestroyed =

    val Entity : Entity

    new (entity) = { Entity = entity }

    interface IECSEvent

type IEntityLookupData =

    abstract Entities : Entity UnsafeResizeArray with get

type ForEachDelegate<'T when 'T :> IECSComponent> = delegate of Entity * byref<'T> -> unit

type ForEachDelegate<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> = delegate of Entity * byref<'T1> * byref<'T2> -> unit

type ForEachDelegate<'T1, 'T2, 'T3 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> -> unit

type ForEachDelegate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent and 'T4 :> IECSComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> * byref<'T4> -> unit

[<ReferenceEquality>]
type EntityLookupData<'T when 'T :> IECSComponent> =
    {
        ComponentAddedEvent: Event<ComponentAdded<'T>>
        ComponentRemovedEvent: Event<ComponentRemoved<'T>>

        RemoveComponent: Entity -> unit

        Active: bool []
        IndexLookup: int []
        Entities: Entity UnsafeResizeArray
        Components: 'T UnsafeResizeArray
    }

    interface IEntityLookupData with

        member this.Entities = this.Entities

[<Sealed>]
type EntityManager (eventManager: EventManager, maxEntityAmount) =
    let maxEntityAmount = maxEntityAmount + 1
    let lookup = Dictionary<Type, IEntityLookupData> ()

    let activeVersions = Array.init maxEntityAmount (fun _ -> 0u)
    let activeIndices = Array.zeroCreate<bool> maxEntityAmount

    let mutable nextEntityIndex = 1
    let removedEntityQueue = Queue<Entity> () 

    let entityRemovals : ((Entity -> unit) ResizeArray) [] = Array.init maxEntityAmount (fun _ -> ResizeArray ())

    let addComponentQueue = ConcurrentQueue<unit -> unit> ()
    let removeComponentQueue = ConcurrentQueue<unit -> unit> ()

    let spawnEntityQueue = ConcurrentQueue<unit -> unit> ()
    let destroyEntityQueue = ConcurrentQueue<unit -> unit> ()

    let finallyDestroyEntityQueue = Queue<unit -> unit> ()

    let emitAddComponentEventQueue = Queue<unit -> unit> ()
    let emitRemoveComponentEventQueue = Queue<unit -> unit> ()

    let emitSpawnEntityEventQueue = Queue<unit -> unit> ()
    let emitDestroyEntityEventQueue = Queue<unit -> unit> ()

    let entitySpawnedEvent = eventManager.GetEvent<EntitySpawned> ()
    let entityDestroyedEvent = eventManager.GetEvent<EntityDestroyed> ()

    let anyComponentAddedEvent = eventManager.GetEvent<AnyComponentAdded> ()
    let anyComponentRemovedEvent = eventManager.GetEvent<AnyComponentRemoved> () 

    let processQueue (queue: Queue<unit -> unit>) =
        while queue.Count > 0 do
            queue.Dequeue () ()

    let processConcurrentQueue (queue: ConcurrentQueue<unit -> unit>) =
        let mutable f = Unchecked.defaultof<unit -> unit>
        while queue.TryDequeue (&f) do
            f ()

    member inline this.IsValidEntity (entity: Entity) =
        not (entity.Index.Equals 0u) && activeVersions.[entity.Index].Equals entity.Version

    member this.Process () =
        while
            not addComponentQueue.IsEmpty       ||
            not removeComponentQueue.IsEmpty    ||
            not spawnEntityQueue.IsEmpty        ||
            not destroyEntityQueue.IsEmpty
                do

            // ******************************************
            // ************* Entity and Component Removal
            // ******************************************
            processConcurrentQueue  destroyEntityQueue
            processConcurrentQueue  removeComponentQueue

            processQueue            finallyDestroyEntityQueue

            processQueue            emitRemoveComponentEventQueue
            processQueue            emitDestroyEntityEventQueue
            // ******************************************

            // ******************************************
            // ************** Entity and Component Adding
            // ******************************************
            processConcurrentQueue  spawnEntityQueue
            processConcurrentQueue  addComponentQueue

            processQueue            emitAddComponentEventQueue
            processQueue            emitSpawnEntityEventQueue
            // ******************************************

    member this.GetEntityLookupData<'T when 'T :> IECSComponent> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (t, &data) then  
            data :?> EntityLookupData<'T>
        else          
            let data =
                {
                    ComponentAddedEvent = eventManager.GetEvent<ComponentAdded<'T>> ()
                    ComponentRemovedEvent = eventManager.GetEvent<ComponentRemoved<'T>> ()

                    RemoveComponent = fun entity -> this.RemoveComponent<'T> entity

                    Active = Array.zeroCreate<bool> maxEntityAmount
                    IndexLookup = Array.init maxEntityAmount (fun _ -> -1) // -1 means that no component exists for that entity
                    Entities = UnsafeResizeArray.Create 1
                    Components = UnsafeResizeArray.Create 1
                }

            lookup.[t] <- data
            data

    member inline this.Iterate<'T when 'T :> IECSComponent> (del: ForEachDelegate<'T>, useParallelism: bool) : unit =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

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

    member inline this.Iterate<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> (del: ForEachDelegate<'T1, 'T2>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T1>, &data1) && lookup.TryGetValue (typeof<'T2>, &data2) then
            let data = [|data1;data2|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let count = data.Entities.Count
            let entities = data.Entities.Buffer

            let inline iter i =
                let entity = entities.[i]

                if activeIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]

                    if comp1Index >= 0 && comp2Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] then
                        del.Invoke (entity, &data1.Components.Buffer.[comp1Index], &data2.Components.Buffer.[comp2Index])

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i

    member inline this.Iterate<'T1, 'T2, 'T3 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent> (del: ForEachDelegate<'T1, 'T2, 'T3>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T1>, &data1) && lookup.TryGetValue (typeof<'T2>, &data2) && 
           lookup.TryGetValue (typeof<'T3>, &data3) then
            let data = [|data1;data2;data3|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>

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

    member inline this.Iterate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent and 'T4 :> IECSComponent> (del: ForEachDelegate<'T1, 'T2, 'T3, 'T4>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        let mutable data4 = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T1>, &data1) && lookup.TryGetValue (typeof<'T2>, &data2) && 
           lookup.TryGetValue (typeof<'T3>, &data3) && lookup.TryGetValue (typeof<'T4>, &data4) then
            let data = [|data1;data2;data3;data4|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>
            let data4 = data4 :?> EntityLookupData<'T4>

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

    // Components

    member this.AddComponent<'T when 'T :> IECSComponent> (entity: Entity) (comp: 'T) =
        addComponentQueue.Enqueue (fun () ->

            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    printfn "ECS WARNING: Component, %s, already added to %A." typeof<'T>.Name entity
                else
                    entityRemovals.[entity.Index].Add (data.RemoveComponent)

                    data.Active.[entity.Index] <- true
                    data.IndexLookup.[entity.Index] <- data.Entities.Count

                    data.Components.Add comp
                    data.Entities.Add entity

                    emitAddComponentEventQueue.Enqueue (fun () ->
                        anyComponentAddedEvent.Trigger (AnyComponentAdded (entity, typeof<'T>))
                        data.ComponentAddedEvent.Trigger (ComponentAdded<'T> (entity))
                    )

            else
                printfn "ECS WARNING: %A is invalid. Cannot add component, %s." entity typeof<'T>.Name

        )

    member this.RemoveComponent<'T when 'T :> IECSComponent> (entity: Entity) =
        removeComponentQueue.Enqueue (fun () ->

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

                    emitRemoveComponentEventQueue.Enqueue (fun () ->
                        anyComponentRemovedEvent.Trigger (AnyComponentRemoved (entity, typeof<'T>))
                        data.ComponentRemovedEvent.Trigger (ComponentRemoved<'T> (entity))
                    )
                else
                    printfn "ECS WARNING: Component, %s, does not exist on %A." typeof<'T>.Name entity

            else
                printfn "ECS WARNING: %A is invalid. Cannot remove component, %s." entity typeof<'T>.Name

        )

    // Entities

    member this.Spawn f =             
        spawnEntityQueue.Enqueue (fun () ->

            if removedEntityQueue.Count = 0 && nextEntityIndex >= maxEntityAmount then
                printfn "ECS WARNING: Unable to spawn entity. Max entity amount hit: %i." (maxEntityAmount - 1)
            else
                let entity =
                    if removedEntityQueue.Count > 0 then
                        let entity = removedEntityQueue.Dequeue ()
                        Entity (entity.Index, entity.Version + 1u)
                    else
                        let index = nextEntityIndex
                        nextEntityIndex <- index + 1
                        Entity (index, 0u)

                activeVersions.[entity.Index] <- entity.Version
                activeIndices.[entity.Index] <- true

                emitSpawnEntityEventQueue.Enqueue (fun () ->
                    entitySpawnedEvent.Trigger (EntitySpawned (entity))
                )

                f entity

        )

    member this.Destroy (entity: Entity) =
        destroyEntityQueue.Enqueue (fun () ->

            if this.IsValidEntity entity then
                let removals = entityRemovals.[entity.Index]
                removals.ForEach (fun f -> f entity)
                removals.Clear ()
                removedEntityQueue.Enqueue entity  

                finallyDestroyEntityQueue.Enqueue (fun () ->
                    activeVersions.[entity.Index] <- 0u
                    activeIndices.[entity.Index] <- false
                )

                emitDestroyEntityEventQueue.Enqueue (fun () ->
                    entityDestroyedEvent.Trigger (EntityDestroyed (entity))
                )
            else
                printfn "ECS WARNING: %A is invalid. Cannot destroy." entity

        )  

    // Component Query

    member this.TryGet<'T when 'T :> IECSComponent> (entity: Entity) : 'T option = 
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.IsValidEntity entity && activeIndices.[entity.Index] && lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if data.Active.[entity.Index] then
                Some data.Components.Buffer.[data.IndexLookup.[entity.Index]]
            else
                None
        else
            None

    member this.TryFind<'T when 'T :> IECSComponent> (f: Entity -> 'T -> bool) : (Entity * 'T) option =
        let mutable result = Unchecked.defaultof<Entity * 'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let count = data.Entities.Count

            for i = 0 to count - 1 do
                let entity = data.Entities.Buffer.[i]
                let comp = data.Components.Buffer.[i]

                if activeIndices.[entity.Index] && data.Active.[entity.Index] && f entity comp then 
                    result <- (entity, comp)
        
        if obj.ReferenceEquals (result, null) then None
        else Some result

    member this.GetAll<'T when 'T :> IECSComponent> () : (Entity * 'T) [] =
        let result = ResizeArray<Entity * 'T> ()

        this.Iterate<'T> ((fun entity x -> result.Add(entity, x)), false)

        result.ToArray ()

    member this.GetAll<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> () : (Entity * 'T1 * 'T2) [] =
        let result = ResizeArray<Entity * 'T1 * 'T2> ()

        this.Iterate<'T1, 'T2> ((fun entity x1 x2 -> result.Add(entity, x1, x2)), false)

        result.ToArray ()

    member this.ForEach<'T when 'T :> IECSComponent> del : unit =
        this.Iterate<'T> (del, false)

    member this.ForEach<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> del : unit =
        this.Iterate<'T1, 'T2> (del, false)

    member this.ForEach<'T1, 'T2, 'T3 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent> del : unit =
        this.Iterate<'T1, 'T2, 'T3> (del, false)

    member this.ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent and 'T4 :> IECSComponent> del : unit =
        this.Iterate<'T1, 'T2, 'T3, 'T4> (del, false)

    member this.ParallelForEach<'T when 'T :> IECSComponent> del : unit =
        this.Iterate<'T> (del, true)

    member this.ParallelForEach<'T1, 'T2 when 'T1 :> IECSComponent and 'T2 :> IECSComponent> del : unit =
        this.Iterate<'T1, 'T2> (del, true)

    member this.ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IECSComponent and 'T2 :> IECSComponent and 'T3 :> IECSComponent> del : unit =
        this.Iterate<'T1, 'T2, 'T3> (del, true)

type Entities = EntityManager