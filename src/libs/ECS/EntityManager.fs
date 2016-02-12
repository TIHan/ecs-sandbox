namespace ECS

open System
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks
open System.Runtime.InteropServices

#nowarn "9"

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

type IComponent = interface end

type ComponentAdded<'T when 'T :> IComponent> = ComponentAdded of Entity with

    interface IEvent

type ComponentRemoved<'T when 'T :> IComponent> = ComponentRemoved of Entity with

    interface IEvent

type AnyComponentAdded = AnyComponentAdded of Entity * componentType: Type with

    interface IEvent

type AnyComponentRemoved = AnyComponentRemoved of Entity * componentType: Type with

    interface IEvent

type EntitySpawned = EntitySpawned of Entity with

    interface IEvent

type EntityDestroyed = EntityDestroyed of Entity with

    interface IEvent

type IEntityLookupData =

    abstract Entities : Entity [] with get

    abstract Count : int with get

type ForEachDelegate<'T when 'T :> IComponent> = delegate of Entity * byref<'T> -> unit

type ForEachDelegate<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> = delegate of Entity * byref<'T1> * byref<'T2> -> unit

type ForEachDelegate<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> -> unit

type ForEachDelegate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> = delegate of Entity * byref<'T1> * byref<'T2> * byref<'T3> * byref<'T4> -> unit

[<ReferenceEquality>]
type EntityLookupData<'T when 'T :> IComponent> =
    {
        ComponentAddedEvent: Event<ComponentAdded<'T>>
        Active: bool []
        Entities: Entity []
        Components: 'T []
        IndexLookup: int []
        mutable Count: int 
    }

    interface IEntityLookupData with

        member this.Entities = this.Entities

        member this.Count = this.Count

[<Sealed>]
type EntityManager (eventAggregator: EventAggregator, maxEntityAmount) =
    let lookup = Dictionary<Type, IEntityLookupData> ()

    // IMPORTANT: The first element will always be 0u. Don't break that rule. :)
    let activeVersions = Array.init maxEntityAmount (fun _ -> 0u)
    let activeIndices = Array.zeroCreate<bool> maxEntityAmount

    // We don't start with index 0 and version 0 due to the possibility of creating an Entity using the default ctor and how we determine what version is active.
    let mutable nextEntityIndex = 1
    let removedEntityQueue = Queue<Entity> () 

    let entityRemovals : ((unit -> unit) ResizeArray) [] = Array.init maxEntityAmount (fun _ -> ResizeArray ())

    let addComponentQueue = ConcurrentQueue<unit -> unit> ()
    let removeComponentQueue = ConcurrentQueue<unit -> unit> ()

    let spawnEntityQueue = ConcurrentQueue<unit -> unit> ()
    let destroyEntityQueue = ConcurrentQueue<unit -> unit> ()

    let finallyDestroyEntityQueue = Queue<unit -> unit> ()

    let emitAddComponentEventQueue = Queue<unit -> unit> ()
    let emitRemoveComponentEventQueue = Queue<unit -> unit> ()

    let emitSpawnEntityEventQueue = Queue<unit -> unit> ()
    let emitDestroyEntityEventQueue = Queue<unit -> unit> ()

    let entitySpawnedEvent : Event<EntitySpawned> = EventAggregator.Unsafe.getEvent eventAggregator
    let anyComponentAddedEvent : Event<AnyComponentAdded> = EventAggregator.Unsafe.getEvent eventAggregator

    let processQueue (queue: Queue<unit -> unit>) =
        while queue.Count > 0 do
            queue.Dequeue () ()

    let processConcurrentQueue (queue: ConcurrentQueue<unit -> unit>) =
        let mutable f = Unchecked.defaultof<unit -> unit>
        while queue.TryDequeue (&f) do
            f ()

    member inline this.IsValidEntity (entity: Entity) =
        not (entity.Version.Equals 0u) && activeVersions.[entity.Index].Equals entity.Version

    member this.Process () =
        while
            not addComponentQueue.IsEmpty       ||
            not removeComponentQueue.IsEmpty    ||
            not spawnEntityQueue.IsEmpty        ||
            not destroyEntityQueue.IsEmpty
                do

            processConcurrentQueue  destroyEntityQueue
            processConcurrentQueue  removeComponentQueue

            processQueue            finallyDestroyEntityQueue

            processConcurrentQueue  spawnEntityQueue
            processConcurrentQueue  addComponentQueue

            processQueue            emitRemoveComponentEventQueue
            processQueue            emitDestroyEntityEventQueue
            processQueue            emitAddComponentEventQueue
            processQueue            emitSpawnEntityEventQueue

    member this.GetEntityLookupData<'T when 'T :> IComponent> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (t, &data) then  
            data :?> EntityLookupData<'T>
        else          
            let data =
                {
                    ComponentAddedEvent = EventAggregator.Unsafe.getEvent eventAggregator
                    Active = Array.zeroCreate<bool> maxEntityAmount
                    Entities = Array.zeroCreate<Entity> maxEntityAmount
                    Components = Array.zeroCreate<'T> maxEntityAmount
                    IndexLookup = Array.init maxEntityAmount (fun _ -> -1) // -1 means that no component exists for that entity
                    Count = 0
                }

            lookup.[t] <- data
            data

    member inline this.Iterate<'T when 'T :> IComponent> (del: ForEachDelegate<'T>, useParallelism: bool) : unit =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let count = data.Count
            let active = data.Active
            let entities = data.Entities
            let components = data.Components

            let inline iter i = 
                let entity = entities.[i]

                if active.[entity.Index] && activeIndices.[entity.Index] then
                    del.Invoke (entity, &components.[i])

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i

    member inline this.Iterate<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> (del: ForEachDelegate<'T1, 'T2>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T1>, &data1) && lookup.TryGetValue (typeof<'T2>, &data2) then
            let data = [|data1;data2|] |> Array.minBy (fun x -> x.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let count = data.Count
            let entities = data.Entities

            let inline iter i =
                let entity = entities.[i]

                if activeIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]

                    if comp1Index >= 0 && comp2Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] then
                        del.Invoke (entity, &data1.Components.[comp1Index], &data2.Components.[comp2Index])

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i

    member inline this.Iterate<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> (del: ForEachDelegate<'T1, 'T2, 'T3>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T1>, &data1) && lookup.TryGetValue (typeof<'T2>, &data2) && 
           lookup.TryGetValue (typeof<'T3>, &data3) then
            let data = [|data1;data2;data3|] |> Array.minBy (fun x -> x.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>

            let count = data.Count
            let entities = data.Entities

            let inline iter i =
                let entity = entities.[i]

                if activeIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]
                    let comp3Index = data3.IndexLookup.[entity.Index]

                    if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] && data3.Active.[entity.Index] then
                        del.Invoke (entity, &data1.Components.[comp1Index], &data2.Components.[comp2Index], &data3.Components.[comp3Index])

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i

    member inline this.Iterate<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> (del: ForEachDelegate<'T1, 'T2, 'T3, 'T4>, useParallelism: bool) : unit =
        let mutable data1 = Unchecked.defaultof<IEntityLookupData>
        let mutable data2 = Unchecked.defaultof<IEntityLookupData>
        let mutable data3 = Unchecked.defaultof<IEntityLookupData>
        let mutable data4 = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T1>, &data1) && lookup.TryGetValue (typeof<'T2>, &data2) && 
           lookup.TryGetValue (typeof<'T3>, &data3) && lookup.TryGetValue (typeof<'T4>, &data4) then
            let data = [|data1;data2;data3;data4|] |> Array.minBy (fun x -> x.Count)
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>
            let data4 = data4 :?> EntityLookupData<'T4>

            let count = data.Count
            let entities = data.Entities

            let inline iter i =
                let entity = entities.[i]

                if activeIndices.[entity.Index] then
                    let comp1Index = data1.IndexLookup.[entity.Index]
                    let comp2Index = data2.IndexLookup.[entity.Index]
                    let comp3Index = data3.IndexLookup.[entity.Index]
                    let comp4Index = data4.IndexLookup.[entity.Index]

                    if comp1Index >= 0 && comp2Index >= 0 && comp3Index >= 0 && comp4Index >= 0 && data1.Active.[entity.Index] && data2.Active.[entity.Index] && data3.Active.[entity.Index] && data4.Active.[entity.Index] then
                        del.Invoke (entity, &data1.Components.[comp1Index], &data2.Components.[comp2Index], &data3.Components.[comp3Index], &data4.Components.[comp4Index])

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i

    // Components

    member this.AddComponent<'T when 'T :> IComponent> (entity: Entity) (comp: 'T) =
        addComponentQueue.Enqueue (fun () ->

            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    printfn "ECS WARNING: Component, %s, already added to %A." typeof<'T>.Name entity
                else
                    entityRemovals.[entity.Index].Add (fun () -> this.RemoveComponent<'T> entity)

                    data.Components.[data.Count] <- comp
                    data.Active.[entity.Index] <- true
                    data.Entities.[data.Count] <- entity
                    data.IndexLookup.[entity.Index] <- data.Count
                    data.Count <- data.Count + 1

                    emitAddComponentEventQueue.Enqueue (fun () ->
                        anyComponentAddedEvent.Trigger (AnyComponentAdded (entity, typeof<'T>))
                        data.ComponentAddedEvent.Trigger (ComponentAdded (entity))
                    )

            else
                printfn "ECS WARNING: %A is invalid. Cannot add component, %s." entity typeof<'T>.Name

        )

    member this.RemoveComponent<'T when 'T :> IComponent> (entity: Entity) =
        removeComponentQueue.Enqueue (fun () ->

            if this.IsValidEntity entity then
                let data = this.GetEntityLookupData<'T> ()

                if data.IndexLookup.[entity.Index] >= 0 then
                    let lastIndex = data.Count - 1
                    let index = data.IndexLookup.[entity.Index]
                    let swappingEntity = data.Entities.[lastIndex]

                    data.Active.[entity.Index] <- false
                    data.Entities.[index] <- swappingEntity
                    data.Components.[index] <- data.Components.[lastIndex]
                    data.IndexLookup.[swappingEntity.Index] <- index
                    data.Components.[lastIndex] <- Unchecked.defaultof<'T>
                    data.IndexLookup.[entity.Index] <- -1
                    data.Count <- data.Count - 1

                    emitRemoveComponentEventQueue.Enqueue (fun () ->
                        eventAggregator.Publish (AnyComponentRemoved (entity, typeof<'T>))
                        eventAggregator.Publish<ComponentRemoved<'T>> (ComponentRemoved (entity))
                    )
                else
                    printfn "ECS WARNING: Component, %s, does not exist on %A." typeof<'T>.Name entity

            else
                printfn "ECS WARNING: %A is invalid. Cannot remove component, %s." entity typeof<'T>.Name

        )

    // Entities

    member this.Spawn f =             
        spawnEntityQueue.Enqueue (fun () ->

            // We don't start with index 0 and version 0 due to the possibility of creating an Entity using the default ctor and how we determine what version is active.
            let entity =
                if removedEntityQueue.Count > 0 then
                    let entity = removedEntityQueue.Dequeue ()
                    Entity (entity.Index, entity.Version + 1u)
                else
                    let index = nextEntityIndex
                    nextEntityIndex <- index + 1
                    Entity (index, 1u)

            if maxEntityAmount <= entity.Index then
                printfn "ECS WARNING: Unable to spawn %A. Max entity amount hit: %i." entity maxEntityAmount
            else
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
                removals.ForEach (fun f -> f ())
                removals.Clear ()
                removedEntityQueue.Enqueue entity  

                finallyDestroyEntityQueue.Enqueue (fun () ->
                    activeVersions.[entity.Index] <- 0u
                    activeIndices.[entity.Index] <- false
                )

                emitDestroyEntityEventQueue.Enqueue (fun () ->
                    eventAggregator.Publish (EntityDestroyed (entity))
                )
            else
                printfn "ECS WARNING: %A is invalid. Cannot destroy." entity

        )  

    // Component Query

    member this.TryGet<'T when 'T :> IComponent> (entity: Entity) : 'T option = 
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if this.IsValidEntity entity && activeIndices.[entity.Index] && lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if data.Active.[entity.Index] then
                Some data.Components.[data.IndexLookup.[entity.Index]]
            else
                None
        else
            None

    member this.TryFind<'T when 'T :> IComponent> (f: Entity -> 'T -> bool) : (Entity * 'T) option =
        let mutable result = Unchecked.defaultof<Entity * 'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>

            let count = data.Count

            for i = 0 to count - 1 do
                let entity = data.Entities.[i]
                let comp = data.Components.[i]

                if activeIndices.[entity.Index] && data.Active.[entity.Index] && f entity comp then 
                    result <- (entity, comp)
        
        if obj.ReferenceEquals (result, null) then None
        else Some result

    member this.GetAll<'T when 'T :> IComponent> () : (Entity * 'T) [] =
        let result = ResizeArray<Entity * 'T> ()

        this.Iterate<'T> ((fun entity x -> result.Add(entity, x)), false)

        result.ToArray ()

    member this.GetAll<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> () : (Entity * 'T1 * 'T2) [] =
        let result = ResizeArray<Entity * 'T1 * 'T2> ()

        this.Iterate<'T1, 'T2> ((fun entity x1 x2 -> result.Add(entity, x1, x2)), false)

        result.ToArray ()

    member this.ForEach<'T when 'T :> IComponent> del : unit =
        this.Iterate<'T> (del, false)

    member this.ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> del : unit =
        this.Iterate<'T1, 'T2> (del, false)

    member this.ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> del : unit =
        this.Iterate<'T1, 'T2, 'T3> (del, false)

    member this.ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> del : unit =
        this.Iterate<'T1, 'T2, 'T3, 'T4> (del, false)

    member this.ParallelForEach<'T when 'T :> IComponent> del : unit =
        this.Iterate<'T> (del, true)

    member this.ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> del : unit =
        this.Iterate<'T1, 'T2> (del, true)

    member this.ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> del : unit =
        this.Iterate<'T1, 'T2, 'T3> (del, true)
