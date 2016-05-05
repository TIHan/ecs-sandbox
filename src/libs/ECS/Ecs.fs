module FSharp.Ecs

open System
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices

#nowarn "9"

type IEntitySystemEvent = interface end

[<ReferenceEquality>]
type EventManager  =
    {
        Lookup: ConcurrentDictionary<Type, obj>
    }

    static member Create () =
        {
            Lookup = ConcurrentDictionary<Type, obj> ()
        }

    member this.Trigger (event: 'T when 'T :> IEntitySystemEvent and 'T : not struct) =
        let mutable value = Unchecked.defaultof<obj>
        if this.Lookup.TryGetValue (typeof<'T>, &value) then
            (value :?> Event<'T>).Trigger event

    member this.GetEvent<'T when 'T :> IEntitySystemEvent> () =
       this.Lookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<'T> () :> obj)) :?> Event<'T>

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

[<Struct>]
type Entity =

    val Index : int

    val Version : uint32

    new (index, version) = { Index = index; Version = version }

    override this.ToString () = String.Format ("(Entity #{0}.{1})", this.Index, this.Version)

type IEntityComponent = interface end

module Events =

    type ComponentAdded<'T when 'T :> IEntityComponent and 'T : not struct> = 
        { entity: Entity }

        member this.Entity = this.entity

        interface IEntitySystemEvent

    type ComponentRemoved<'T when 'T :> IEntityComponent and 'T : not struct> = 
        { entity: Entity }

        member this.Entity = this.entity

        interface IEntitySystemEvent

    type EntitySpawned =
        { entity: Entity }

        member this.Entity = this.entity

        interface IEntitySystemEvent

    type EntityDestroyed =
        { entity: Entity }

        member this.Entity = this.entity

        interface IEntitySystemEvent

open Events

type IEntityLookupData =

    abstract Entities : Entity UnsafeResizeArray with get

    abstract Remove : Entity -> unit

[<ReferenceEquality>]
type EntityLookupData<'T when 'T :> IEntityComponent and 'T : not struct> =
    {
        ComponentAddedEvent: Event<ComponentAdded<'T>>
        ComponentRemovedEvent: Event<ComponentRemoved<'T>>

        Active: bool []
        IndexLookup: int []
        Entities: Entity UnsafeResizeArray
        Components: 'T UnsafeResizeArray
    }

    member data.Add (entity: Entity) (comp: 'T) =
        if data.IndexLookup.[entity.Index] >= 0 then
            printfn "ECS WARNING: Component, %s, already added to %A." typeof<'T>.Name entity
        else
            data.Active.[entity.Index] <- true
            data.IndexLookup.[entity.Index] <- data.Entities.Count

            data.Components.Add comp
            data.Entities.Add entity

            data.ComponentAddedEvent.Trigger ({ entity = entity })

    interface IEntityLookupData with

        member this.Entities = this.Entities

        member data.Remove entity =
            if data.IndexLookup.[entity.Index] >= 0 then
                let index = data.IndexLookup.[entity.Index]
                let swappingEntity = data.Entities.LastItem

                data.Entities.SwapRemoveAt index
                data.Components.SwapRemoveAt index

                data.Active.[entity.Index] <- false
                data.IndexLookup.[entity.Index] <- -1

                if not (entity.Index.Equals swappingEntity.Index) then
                    data.IndexLookup.[swappingEntity.Index] <- index

                data.ComponentRemovedEvent.Trigger ({ entity = entity })
            else
                printfn "ECS WARNING: Component, %s, does not exist on %A." typeof<'T>.Name entity

[<ReferenceEquality>]
type EntityManager =
    {
        EventManager: EventManager

        MaxEntityAmount: int
        Lookup: ConcurrentDictionary<Type, IEntityLookupData>

        ActiveVersions: uint32 []
        ActiveIndices: bool []

        mutable NextEntityIndex: int
        RemovedEntityQueue: Queue<Entity>

        EntitySpawnedEvent: Event<EntitySpawned>
        EntityDestroyedEvent: Event<EntityDestroyed>

        ThreadId: int

        mutable CurrentIterations: int
        PendingEntityDestroyQueue: Queue<unit -> unit>
        PendingComponentRemoveQueue: Queue<unit -> unit>
        PendingEntitySpawnQueue: Queue<unit -> unit>
        PendingComponentAddQueue: Queue<unit -> unit>
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

        let entitySpawnedEvent = eventManager.GetEvent<EntitySpawned> ()
        let entityDestroyedEvent = eventManager.GetEvent<EntityDestroyed> ()

        {
            EventManager = eventManager
            MaxEntityAmount = maxEntityAmount
            Lookup = lookup
            ActiveVersions = activeVersions
            ActiveIndices = activeIndices
            NextEntityIndex = nextEntityIndex
            RemovedEntityQueue = removedEntityQueue
            EntitySpawnedEvent = entitySpawnedEvent
            EntityDestroyedEvent = entityDestroyedEvent
            ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId
            CurrentIterations = 0
            PendingEntityDestroyQueue = Queue ()
            PendingComponentRemoveQueue = Queue ()
            PendingEntitySpawnQueue = Queue ()
            PendingComponentAddQueue = Queue ()
        }

    member inline this.IsValidEntity (entity: Entity) =
        not (entity.Index.Equals 0u) && this.ActiveVersions.[entity.Index].Equals entity.Version

    member inline this.ResolvePendingQueues () =
        if this.CurrentIterations = 0 then

            while this.PendingEntityDestroyQueue.Count > 0 do
                let f = this.PendingEntityDestroyQueue.Dequeue ()
                f ()

            while this.PendingComponentRemoveQueue.Count > 0 do
                let f = this.PendingComponentRemoveQueue.Dequeue ()
                f ()

            while this.PendingEntitySpawnQueue.Count > 0 do
                let f = this.PendingEntitySpawnQueue.Dequeue ()
                f ()

            while this.PendingComponentAddQueue.Count > 0 do
                let f = this.PendingComponentAddQueue.Dequeue ()
                f ()

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

                        Active = Array.zeroCreate<bool> this.MaxEntityAmount
                        IndexLookup = Array.init this.MaxEntityAmount (fun _ -> -1) // -1 means that no component exists for that entity
                        Entities = UnsafeResizeArray.Create 1
                        Components = UnsafeResizeArray.Create 1
                    }

                data :> IEntityLookupData

            this.Lookup.GetOrAdd(t, factory) :?> EntityLookupData<'T>

    member inline this.Iterate<'T when 'T :> IEntityComponent and 'T : not struct> (f) : unit =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let inline iter i =
                let entity = data.Entities.Buffer.[i]

                if data.Active.[entity.Index] && this.ActiveIndices.[entity.Index] then
                    f entity data.Components.Buffer.[i]

            for i = 0 to data.Entities.Count - 1 do iter i

    member inline this.Iterate<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> (f) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        if this.Lookup.TryGetValue (typeof<'T1>, &data1) && this.Lookup.TryGetValue (typeof<'T2>, &data2) then
            let data = [|data1;data2|] |> Array.minBy (fun x -> x.Entities.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let inline iter i =
                let entity = data.Entities.Buffer.[i]

                if this.ActiveIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]

                    if comp1Index >= 0 && comp2Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] then
                        f entity data1.Components.Buffer.[comp1Index] data2.Components.Buffer.[comp2Index]

            for i = 0 to data.Entities.Count - 1 do iter i

    member this.AddComponent<'T when 'T :> IEntityComponent and 'T : not struct> (entity: Entity) (comp: 'T) =
        if this.ThreadId <> System.Threading.Thread.CurrentThread.ManagedThreadId then
            failwith "Wrong thread."

        if this.CurrentIterations > 0 then
            this.PendingComponentAddQueue.Enqueue (fun () -> this.AddComponent entity comp)
        else
            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()
                data.Add entity comp
            else
                printfn "ECS WARNING: %A is invalid. Cannot add component, %s." entity typeof<'T>.Name

    member this.RemoveComponent<'T when 'T :> IEntityComponent and 'T : not struct> (entity: Entity) =
        if this.ThreadId <> System.Threading.Thread.CurrentThread.ManagedThreadId then
            failwith "Wrong thread."

        if this.CurrentIterations > 0 then
            this.PendingComponentRemoveQueue.Enqueue (fun () -> this.RemoveComponent<'T> (entity))
        else
            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> () :> IEntityLookupData
                data.Remove (entity)
            else
                printfn "ECS WARNING: %A is invalid. Cannot remove component, %s." entity typeof<'T>.Name

    member this.Spawn f =
        if this.ThreadId <> System.Threading.Thread.CurrentThread.ManagedThreadId then
            failwith "Wrong thread."
                         
        if this.CurrentIterations > 0 then
            this.PendingEntitySpawnQueue.Enqueue (fun () -> this.Spawn f)
        else
        if this.RemovedEntityQueue.Count = 0 && this.NextEntityIndex >= this.MaxEntityAmount then
            printfn "ECS WARNING: Unable to spawn entity. Max entity amount hit: %i." (this.MaxEntityAmount - 1)
        else
            let entity =
                if this.RemovedEntityQueue.Count > 0 then
                    let entity = this.RemovedEntityQueue.Dequeue ()
                    Entity (entity.Index, entity.Version + 1u)
                else
                    let index = this.NextEntityIndex
                    this.NextEntityIndex <- index + 1
                    Entity (index, 1u)

            this.ActiveVersions.[entity.Index] <- entity.Version
            this.ActiveIndices.[entity.Index] <- true

            this.EntitySpawnedEvent.Trigger ({ entity = entity })

            f entity

    member this.Destroy (entity: Entity) =
        if this.ThreadId <> System.Threading.Thread.CurrentThread.ManagedThreadId then
            failwith "Wrong thread."

        if this.CurrentIterations > 0 then
            this.PendingEntityDestroyQueue.Enqueue (fun () -> this.Destroy (entity))
        else
            if this.IsValidEntity entity then
                this.RemovedEntityQueue.Enqueue entity  

                this.ActiveVersions.[entity.Index] <- 0u
                this.ActiveIndices.[entity.Index] <- false

                this.EntityDestroyedEvent.Trigger ({ entity = entity })
            else
                printfn "ECS WARNING: %A is invalid. Cannot destroy." entity

    member this.ForEach<'T when 'T :> IEntityComponent and 'T : not struct> (f: Entity -> 'T -> unit) : unit =
        if this.ThreadId <> System.Threading.Thread.CurrentThread.ManagedThreadId then
            failwith "Wrong thread."

        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

    member this.ForEach<'T1, 'T2 when 'T1 :> IEntityComponent and 'T2 :> IEntityComponent and 'T1 : not struct and 'T2 : not struct> f : unit =
        if this.ThreadId <> System.Threading.Thread.CurrentThread.ManagedThreadId then
            failwith "Wrong thread."

        this.CurrentIterations <- this.CurrentIterations + 1

        this.Iterate<'T1, 'T2> (f)

        this.CurrentIterations <- this.CurrentIterations - 1
        this.ResolvePendingQueues ()

type ISystem<'T> =

    abstract Init : EntityManager * EventManager -> ('T -> unit)

type World<'T> (maxEntityAmount, systems: ISystem<'T> seq) =
    let eventManager = EventManager.Create ()
    let entityManager = EntityManager.Create (eventManager, maxEntityAmount)
    let updates =
        systems
        |> Seq.map (fun sys -> sys.Init (entityManager, eventManager))
        |> Array.ofSeq

    member this.Update data =
        updates
        |> Array.iter (fun update -> update data)
    