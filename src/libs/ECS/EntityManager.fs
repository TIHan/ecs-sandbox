﻿namespace ECS.Core

open System
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks

[<Struct>]
type Entity =

    val Id : int

    new (id) = { Id = id }

type IComponent = interface end

[<AllowNullLiteral>]
type IEntityLookupData = interface end

type EntityLookupData<'T> =
    {
        Active: bool []
        Entities: Entity ResizeArray
        Components: 'T []
    }

    interface IEntityLookupData

[<Sealed>]
type ComponentAdded<'T when 'T :> IComponent> (ent: Entity, com: 'T) =

    member __.Entity = ent

    member __.Component = com

    interface IEvent

[<Sealed>]
type ComponentRemoved<'T when 'T :> IComponent> (ent: Entity, com: 'T) =

    member __.Entity = ent

    member __.Component = com

    interface IEvent

[<Sealed>]
type AnyComponentAdded (ent: Entity, com: IComponent) =
    let typ = com.GetType ()

    member __.Entity = ent

    member __.Component = com

    member __.ComponentType = typ

    interface IEvent

[<Sealed>]
type AnyComponentRemoved (ent: Entity, com: IComponent) =
    let typ = com.GetType ()

    member __.Entity = ent

    member __.Component = com

    member __.ComponentType = typ

    interface IEvent

[<Sealed>]
type EntitySpawned (ent: Entity) =

    member __.Entity = ent

    interface IEvent

[<Sealed>]
type EntityDestroyed (ent: Entity) =

    member __.Entity = ent

    interface IEvent

[<Sealed>]
type EntityManager (eventAggregator: EventAggregator, entityAmount) =
    let lookup = Dictionary<Type, IEntityLookupData> ()
    let active = Array.init entityAmount (fun _ -> false)

    let mutable nextEntity = Entity 0
    let removedEntityQueue = Queue<Entity> () 

    let entityRemovals : ((unit -> unit) ResizeArray) [] = Array.init entityAmount (fun _ -> ResizeArray ())

    let addComponentQueue = ConcurrentQueue<unit -> unit> ()
    let removeComponentQueue = ConcurrentQueue<unit -> unit> ()

    let spawnEntityQueue = ConcurrentQueue<unit -> unit> ()
    let destroyEntityQueue = ConcurrentQueue<unit -> unit> ()

    let activateEntityQueue = Queue<unit -> unit> ()

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
            processConcurrentQueue  spawnEntityQueue

            processConcurrentQueue  removeComponentQueue
            processConcurrentQueue  addComponentQueue

            processQueue            activateEntityQueue

            processQueue            emitRemoveComponentEventQueue
            processQueue            emitDestroyEntityEventQueue
            processQueue            emitAddComponentEventQueue
            processQueue            emitSpawnEntityEventQueue

    member this.GetEntityLookupData<'T> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = null
        if not <| lookup.TryGetValue (t, &data) then
            let active = Array.init entityAmount (fun _ -> false)
            let entities = ResizeArray (entityAmount)
            let components = Array.init<'T> entityAmount (fun _ -> Unchecked.defaultof<'T>)
            
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
        let mutable data = null
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if (entity.Id >= 0 && entity.Id < data.Components.Length) then
                c <- data.Components.[entity.Id]

    member this.TryGetInternal<'T when 'T :> IComponent> (entity: Entity, c: byref<IComponent>) = 
        let mutable data = null
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if (entity.Id >= 0 && entity.Id < data.Components.Length) then
                c <- data.Components.[entity.Id]

    member this.TryFindInternal<'T> (f: Entity -> 'T -> bool, result: byref<Entity * 'T>) =
        let mutable data = null
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            let count = data.Entities.Count

            let mutable n = 0
            while not (n.Equals count) do    
                let entity = data.Entities.[n]
                let comp = data.Components.[entity.Id]

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
                    data.Active.[entity.Id] &&
                    predicate entity.Id
                    then
                    let com = data.Components.[entity.Id]
                    f entity com

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i
        | _ -> ()

    member inline this.IterateInternal<'T1, 'T2> (f: Entity -> 'T1 -> 'T2 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2> with
        | (false,_),_
        | _,(false,_) -> ()
        | (_,data1),(_,data2) ->
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let entities =
                [|data1.Entities;data2.Entities|] |> Array.minBy (fun x -> x.Count)

            for i = 0 to entities.Count - 1 do
                let entity = entities.[i]

                if 
                    data1.Active.[entity.Id] && 
                    data2.Active.[entity.Id] &&
                    predicate entity.Id
                    then
                    let com1 = data1.Components.[entity.Id]
                    let com2 = data2.Components.[entity.Id]
                    f entity com1 com2

    member inline this.IterateInternal<'T1, 'T2, 'T3> (f: Entity -> 'T1 -> 'T2 -> 'T3 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2>, lookup.TryGetValue typeof<'T3> with
        | (false,_),_,_
        | _,(false,_),_
        | _,_,(false,_) -> ()
        | (_,data1),(_,data2),(_,data3) ->
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>

            let entities =
                [|data1.Entities;data2.Entities;data3.Entities|] |> Array.minBy (fun x -> x.Count)

            for i = 0 to entities.Count - 1 do
                let entity = entities.[i]

                if 
                    data1.Active.[entity.Id] && 
                    data2.Active.[entity.Id] &&
                    data3.Active.[entity.Id] &&
                    predicate entity.Id
                    then
                    let com1 = data1.Components.[entity.Id]
                    let com2 = data2.Components.[entity.Id]
                    let com3 = data3.Components.[entity.Id]
                    f entity com1 com2 com3

    member inline this.IterateInternal<'T1, 'T2, 'T3, 'T4> (f: Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2>, lookup.TryGetValue typeof<'T3>, lookup.TryGetValue typeof<'T4> with
        | (false,_),_,_,_
        | _,(false,_),_,_
        | _,_,(false,_),_
        | _,_,_,(false,_) -> ()
        | (_,data1),(_,data2),(_,data3),(_,data4) ->
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>
            let data3 = data3 :?> EntityLookupData<'T3>
            let data4 = data4 :?> EntityLookupData<'T4>

            let entities =
                [|data1.Entities;data2.Entities;data3.Entities;data4.Entities|] |> Array.minBy (fun x -> x.Count)

            for i = 0 to entities.Count - 1 do
                let entity = entities.[i]

                if 
                    data1.Active.[entity.Id] && 
                    data2.Active.[entity.Id] &&
                    data3.Active.[entity.Id] &&
                    data4.Active.[entity.Id] &&
                    predicate entity.Id
                    then
                    let com1 = data1.Components.[entity.Id]
                    let com2 = data2.Components.[entity.Id]
                    let com3 = data3.Components.[entity.Id]
                    let com4 = data4.Components.[entity.Id]
                    f entity com1 com2 com3 com4

    // Components

    member this.AddComponent<'T when 'T :> IComponent> (entity: Entity) (comp: 'T) =
        addComponentQueue.Enqueue (fun () ->
            if active.[entity.Id] then
                failwithf "Entity, #%i, has already spawned. Cannot add component, %s." entity.Id typeof<'T>.Name

            let data = this.GetEntityLookupData<'T> ()

            if data.Active.[entity.Id] then
                printfn "ECS WARNING: Component, %s, already added to Entity, #%i." typeof<'T>.Name entity.Id
            else
                entityRemovals.[entity.Id].Add (fun () -> this.RemoveComponent<'T> entity)

                data.Active.[entity.Id] <- true
                data.Entities.Add entity
                data.Components.[entity.Id] <- comp

                emitAddComponentEventQueue.Enqueue (fun () ->
                    eventAggregator.Publish (AnyComponentAdded (entity, comp))
                    eventAggregator.Publish (ComponentAdded (entity, comp))
                )
        )

    member this.RemoveComponent<'T when 'T :> IComponent> (entity: Entity) =
        removeComponentQueue.Enqueue (fun () ->
            let data = this.GetEntityLookupData<'T> ()

            if data.Active.[entity.Id] then
                let comp = data.Components.[entity.Id]

                data.Active.[entity.Id] <- false
                data.Entities.Remove entity |> ignore
                data.Components.[entity.Id] <- Unchecked.defaultof<'T>

                emitRemoveComponentEventQueue.Enqueue (fun () ->
                    eventAggregator.Publish (AnyComponentRemoved (entity, comp))
                    eventAggregator.Publish (ComponentRemoved (entity, comp))
                )
        )

    // Entities

    member this.Spawn f =             
        spawnEntityQueue.Enqueue (fun () ->
            let entity =
                if removedEntityQueue.Count > 0 then
                    let entity = nextEntity
                    nextEntity <- Entity (entity.Id + 1)
                    entity
                else
                    removedEntityQueue.Dequeue ()

            if entityAmount <= entity.Id then
                printfn "ECS WARNING: Unable to spawn entity, #%i. Max entity count hit." entity.Id
            else
                f entity

                activateEntityQueue.Enqueue (fun () ->
                    active.[entity.Id] <- true
                )

                emitSpawnEntityEventQueue.Enqueue (fun () ->
                    eventAggregator.Publish (EntitySpawned entity)
                )
        )

    member this.Destroy (entity: Entity) =
        destroyEntityQueue.Enqueue (fun () ->
            if active.[entity.Id] then
                active.[entity.Id] <- false

                let removals = entityRemovals.[entity.Id]
                removals.ForEach (fun f -> f ())
                removals.Clear ()
                removedEntityQueue.Enqueue entity  

                emitDestroyEntityEventQueue.Enqueue (fun () ->
                    eventAggregator.Publish (EntityDestroyed entity)
                )
        )  

    // Component Query

    member this.Has<'T when 'T :> IComponent> (entity: Entity) : bool =
        let mutable c = Unchecked.defaultof<'T>
        this.TryGetInternal<'T> (entity, &c)
        obj.ReferenceEquals (c, null)

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
