namespace ECS.Core

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

type IEntityLookupData = interface end

[<ReferenceEquality>]
type EntityLookupData<'T> =
    {
        Active: bool []
        Entities: Entity ResizeArray
        Components: 'T []
    }

    interface IEntityLookupData

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

[<Sealed>]
type EntityManager (eventAggregator: EventAggregator, maxEntityAmount) =
    let lookup = Dictionary<Type, IEntityLookupData> ()

    // IMPORTANT: The first element will always be 0u. Don't break that rule. :)
    let activeVersions = Array.init maxEntityAmount (fun _ -> 0u)

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

    let processQueue (queue: Queue<unit -> unit>) =
        while queue.Count > 0 do
            queue.Dequeue () ()

    let processConcurrentQueue (queue: ConcurrentQueue<unit -> unit>) =
        let mutable f = Unchecked.defaultof<unit -> unit>
        while queue.TryDequeue (&f) do
            f ()

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

    member this.GetEntityLookupData<'T> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if not <| lookup.TryGetValue (t, &data) then
            let active = Array.init maxEntityAmount (fun _ -> false)
            let entities = ResizeArray (maxEntityAmount)
            let components = Array.init<'T> maxEntityAmount (fun _ -> Unchecked.defaultof<'T>)
            
            let data =
                {
                    Active = active
                    Entities = entities
                    Components = components
                }

            lookup.[t] <- data
            data
        else
            data :?> EntityLookupData<'T>

    member this.TryGetInternal<'T> (entity: Entity, c: byref<'T>) = 
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if (entity.Index >= 0 && entity.Index < data.Components.Length) then
                c <- data.Components.[entity.Index]

    member this.TryGetInternal<'T when 'T :> IComponent> (entity: Entity, c: byref<IComponent>) = 
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if (entity.Index >= 0 && entity.Index < data.Components.Length) then
                c <- data.Components.[entity.Index]

    member this.TryFindInternal<'T> (f: Entity -> 'T -> bool, result: byref<Entity * 'T>) =
        let mutable data = Unchecked.defaultof<IEntityLookupData>
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            let count = data.Entities.Count

            let mutable n = 0
            while not (n.Equals count) do    
                let entity = data.Entities.[n]
                let comp = data.Components.[entity.Index]

                if f entity comp then result <- (entity, comp)
                n <- n + 1  

    member inline this.IterateInternal<'T> (f: Entity -> 'T -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T> with
        | (true, data) ->
            let data = data :?> EntityLookupData<'T>

            let count = data.Entities.Count

            let inline iter i =
                let entity = data.Entities.[i]

                if
                    data.Active.[entity.Index] &&
                    predicate entity.Index
                        then
                    let com = data.Components.[entity.Index]
                    f entity com

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i
        | _ -> ()

    member inline this.IterateInternal<'T1, 'T2> (f: Entity -> 'T1 -> 'T2 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2> with
        | (true, data1), (true, data2) ->
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let entities =
                [|data1.Entities;data2.Entities|] |> Array.minBy (fun x -> x.Count)

            let count = entities.Count

            let inline iter i =
                let entity = entities.[i]

                if
                    data1.Active.[entity.Index] &&
                    data2.Active.[entity.Index] &&
                    predicate entity.Index
                        then
                    let com1 = data1.Components.[entity.Index]
                    let com2 = data2.Components.[entity.Index]
                    f entity com1 com2

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i
        | _ -> ()

    member inline this.IterateInternal<'T1, 'T2, 'T3> (f: Entity -> 'T1 -> 'T2 -> 'T3 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2>, lookup.TryGetValue typeof<'T3> with
        | (true, data1), (true, data2), (true, data3) ->
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>

            let entities =
                [|data1.Entities;data2.Entities;data3.Entities|] |> Array.minBy (fun x -> x.Count)

            let count = entities.Count

            let inline iter i =
                let entity = entities.[i]

                if
                    data1.Active.[entity.Index] &&
                    data2.Active.[entity.Index] &&
                    data3.Active.[entity.Index] &&
                    predicate entity.Index
                        then
                    let com1 = data1.Components.[entity.Index]
                    let com2 = data2.Components.[entity.Index]
                    let com3 = data3.Components.[entity.Index]
                    f entity com1 com2 com3

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i
          | _ -> ()

    member inline this.IterateInternal<'T1, 'T2, 'T3, 'T4> (f: Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2>, lookup.TryGetValue typeof<'T3>, lookup.TryGetValue typeof<'T4> with
        | (true, data1), (true, data2), (true, data3), (true, data4) ->
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>
            let data4 = data4 :?> EntityLookupData<'T4>

            let entities =
                [|data1.Entities;data2.Entities;data3.Entities;data4.Entities|] |> Array.minBy (fun x -> x.Count)

            let count = entities.Count

            let inline iter i =
                let entity = entities.[i]

                if
                    data1.Active.[entity.Index] &&
                    data2.Active.[entity.Index] &&
                    data3.Active.[entity.Index] &&
                    data4.Active.[entity.Index] &&
                    predicate entity.Index
                        then
                    let com1 = data1.Components.[entity.Index]
                    let com2 = data2.Components.[entity.Index]
                    let com3 = data3.Components.[entity.Index]
                    let com4 = data4.Components.[entity.Index]
                    f entity com1 com2 com3 com4

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i
         | _ -> ()

    // Components

    member this.AddComponent<'T when 'T :> IComponent> (entity: Entity) (comp: 'T) =
        addComponentQueue.Enqueue (fun () ->

            if not (entity.Version.Equals 0u) && activeVersions.[entity.Index].Equals entity.Version then
                let data = this.GetEntityLookupData<'T> ()

                if data.Active.[entity.Index] then
                    printfn "ECS WARNING: Component, %s, already added to %A." typeof<'T>.Name entity
                else
                    entityRemovals.[entity.Index].Add (fun () -> this.RemoveComponent<'T> entity)

                    data.Active.[entity.Index] <- true
                    data.Entities.Add entity
                    data.Components.[entity.Index] <- comp

                    emitAddComponentEventQueue.Enqueue (fun () ->
                        eventAggregator.Publish (AnyComponentAdded (entity, typeof<'T>))
                        eventAggregator.Publish<ComponentAdded<'T>> (ComponentAdded (entity))
                    )

            else
                printfn "ECS WARNING: %A is invalid. Cannot add component, %s." entity typeof<'T>.Name

        )

    member this.RemoveComponent<'T when 'T :> IComponent> (entity: Entity) =
        removeComponentQueue.Enqueue (fun () ->

            if not (entity.Version.Equals 0u) && activeVersions.[entity.Index].Equals entity.Version then
                let data = this.GetEntityLookupData<'T> ()

                if data.Active.[entity.Index] then
                    let comp = data.Components.[entity.Index]

                    data.Active.[entity.Index] <- false
                    data.Entities.Remove entity |> ignore
                    data.Components.[entity.Index] <- Unchecked.defaultof<'T>

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
                    let index = nextEntityIndex
                    nextEntityIndex <- index + 1
                    Entity (index, 1u)
                else
                    let entity = removedEntityQueue.Dequeue ()
                    Entity (entity.Index, entity.Version + 1u)

            if maxEntityAmount <= entity.Index then
                printfn "ECS WARNING: Unable to spawn %A. Max entity amount hit: %i." entity maxEntityAmount
            else
                f entity

                activeVersions.[entity.Index] <- entity.Version

                emitSpawnEntityEventQueue.Enqueue (fun () ->
                    eventAggregator.Publish (EntitySpawned (entity))
                )

        )

    member this.Destroy (entity: Entity) =
        destroyEntityQueue.Enqueue (fun () ->

            if not (entity.Version.Equals 0u) && activeVersions.[entity.Index].Equals entity.Version then
                let removals = entityRemovals.[entity.Index]
                removals.ForEach (fun f -> f ())
                removals.Clear ()
                removedEntityQueue.Enqueue entity  

                finallyDestroyEntityQueue.Enqueue (fun () ->
                    activeVersions.[entity.Index] <- 0u
                )

                emitDestroyEntityEventQueue.Enqueue (fun () ->
                    eventAggregator.Publish (EntityDestroyed (entity))
                )
            else
                printfn "ECS WARNING: %A is invalid. Cannot destroy." entity

        )  

    // Component Query

    member this.TryGet (entity: Entity, t: Type) : IComponent option =
        let mutable c = Unchecked.defaultof<IComponent>
        this.TryGetInternal (entity, &c)
        if obj.ReferenceEquals (c, null) then None
        else Some c

    member this.TryGet (entity, c: byref<#IComponent>) =
        this.TryGetInternal (entity, &c)

    member this.TryGet<'T when 'T :> IComponent> (entity: Entity) : 'T option = 
        let mutable c = Unchecked.defaultof<'T>
        this.TryGet<'T> (entity, &c)

        if obj.ReferenceEquals (c, null) then None
        else Some c

    member this.TryFind<'T when 'T :> IComponent> (f: Entity -> 'T -> bool) : (Entity * 'T) option =
        let mutable result = Unchecked.defaultof<Entity * 'T>
        this.TryFindInternal (f, &result)
        
        if obj.ReferenceEquals (result, null) then None
        else Some result

    member this.GetAll<'T when 'T :> IComponent> () : (Entity * 'T) [] =
        let result = ResizeArray<Entity * 'T> ()

        this.IterateInternal<'T> ((fun entity x -> result.Add(entity, x)), false, fun _ -> true)

        result.ToArray ()

    member this.GetAll<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> () : (Entity * 'T1 * 'T2) [] =
        let result = ResizeArray<Entity * 'T1 * 'T2> ()

        this.IterateInternal<'T1, 'T2> ((fun entity x1 x2 -> result.Add(entity, x1, x2)), false, fun _ -> true)

        result.ToArray ()

    member this.ForEach<'T when 'T :> IComponent> f : unit =
        this.IterateInternal<'T> (f, false, fun _ -> true)

    member this.ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
        this.IterateInternal<'T1, 'T2> (f, false, fun _ -> true)

    member this.ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> f : unit =
        this.IterateInternal<'T1, 'T2, 'T3> (f, false, fun _ -> true)

    member this.ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> f : unit =
        this.IterateInternal<'T1, 'T2, 'T3, 'T4> (f, false, fun _ -> true)

    member this.ParallelForEach<'T when 'T :> IComponent> f : unit =
        this.IterateInternal<'T> (f, true, fun _ -> true)

    member this.ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
        this.IterateInternal<'T1, 'T2> (f, true, fun _ -> true)

    member this.ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> f : unit =
        this.IterateInternal<'T1, 'T2, 'T3> (f, true, fun _ -> true)
