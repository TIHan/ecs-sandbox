﻿namespace ECS.Core

open System
open System.Reflection
open System.Collections.Generic
open System.Threading.Tasks

type EntityEvent =
    | Created of Entity
    | Spawned of Entity
    | Destroyed of Entity

    interface IEvent

type ComponentEvent =
    | AnyAdded of Entity * obj * Type
    | AnyRemoved of Entity * obj * Type

    interface IEvent

type ComponentEvent<'T> =
    | Added of Entity * 'T
    | Removed of Entity * 'T

    interface IEvent

type IComponentQuery =

    abstract Has<'T when 'T :> IComponent> : Entity -> bool

    abstract TryGet<'T when 'T :> IComponent> : Entity -> 'T option

    abstract TryFind<'T when 'T :> IComponent> : (Entity * 'T -> bool) -> (Entity * 'T) option

    abstract Get<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract Get<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []

    abstract ForEach<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 * 'T4 -> unit) -> unit

    abstract ParallelForEach<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit

type IComponentService =

    abstract Add<'T when 'T :> IComponent> : Entity -> 'T -> unit

    abstract Remove<'T when 'T :> IComponent> : Entity -> unit

type IEntityService =

    abstract Create : unit -> Entity

    abstract Destroy : Entity -> unit

type EntityLookupData =
    {
        entities: Entity ResizeArray
        entitySet: Entity HashSet
        components: obj []
    }

[<Sealed>]
type EntityManager (eventAggregator: IEventAggregator, entityAmount) =
    let lookup = Dictionary<Type, EntityLookupData> ()
    let lockObj = obj ()
    let deferQueue = MessageQueue<unit -> unit> ()
    let deferPreEntityEventQueue = MessageQueue<EntityEvent> ()
    let deferComponentEventQueue = MessageQueue<unit -> unit> ()
    let deferEntityEventQueue = MessageQueue<EntityEvent> ()
    let deferDispose = MessageQueue<obj> ()
    let disposableTypeInfo = typeof<IDisposable>.GetTypeInfo()

    let eventAggregatorType = typeof<IEventAggregator>
    let publishMethod = eventAggregatorType.GetRuntimeMethods () |> Seq.find (fun x -> x.Name = "Publish")

    let componentEventType = Type.GetType ("ECS.Core.ComponentEvent`1")
    let componentAddedType = Type.GetType ("ECS.Core.ComponentEvent`1+Added")
    let componentRemovedType = Type.GetType ("ECS.Core.ComponentEvent`1+Removed")

    let mutable nextEntityId = 0

    let publishComponentAdded entity comp (t: Type) =
        let ctor = componentAddedType.MakeGenericType(t).GetTypeInfo().DeclaredConstructors |> Seq.head
        let m = publishMethod.MakeGenericMethod (componentEventType.MakeGenericType (t))
        let e = ctor.Invoke(parameters = [|entity;comp|])
        m.Invoke (eventAggregator, [|e|]) |> ignore
        eventAggregator.Publish (AnyAdded (entity, comp, t))

    let publishComponentRemoved entity comp (t: Type) =
        let eventType = componentRemovedType.MakeGenericType(t)
        let ctor = eventType.GetTypeInfo().DeclaredConstructors |> Seq.head
        let m = publishMethod.MakeGenericMethod (eventType)
        let e = ctor.Invoke(parameters = [|entity;comp|])
        m.Invoke (eventAggregator, [|e|]) |> ignore
        eventAggregator.Publish (AnyRemoved (entity, comp, t))

    member inline this.Defer f =
        deferQueue.Push f

    member inline this.DeferPreEntityEvent x =
        deferPreEntityEventQueue.Push x

    member inline this.DeferComponentEvent f =
        deferComponentEventQueue.Push f

    member inline this.DeferEntityEvent x =
        deferEntityEventQueue.Push x

    member inline this.DeferDispose x =
        deferDispose.Push x

    member this.LoadComponent (t: Type) =
        lock lockObj <| fun () ->
            match lookup.TryGetValue t with
            | false, _ ->
                let entities = ResizeArray ()
                let entitySet = HashSet ()
                let components = Array.init entityAmount (fun _ -> null)
                let data =
                    {
                        entities = entities
                        entitySet = entitySet
                        components = components
                    }

                lookup.[t] <- data
            | _ -> ()  

    member this.Create () : Entity =
        let entity = 
            lock lockObj <| fun () ->
                let entity = Entity nextEntityId
                nextEntityId <- nextEntityId + 1
                entity

        this.DeferPreEntityEvent (Created entity)
        this.DeferEntityEvent (Spawned entity)
        entity

    member this.Destroy (entity: Entity) =
        this.RemoveAllComponents (entity)     

    member this.AddComponent (entity: Entity, comp: obj, t: Type) =
        this.LoadComponent (t)

        let data = lookup.[t]

        if data.entitySet.Add entity then
            data.entities.Add entity
            this.DeferComponentEvent <| fun () -> publishComponentAdded entity comp t

        data.components.[entity.Id] <- comp
        
    member this.TryRemoveComponent (entity: Entity, t: Type) : obj option =
        match lookup.TryGetValue t with
        | false, _ -> None
        | _, data ->
            data.entitySet.Remove entity |> ignore
            data.entities.Remove entity |> ignore
            if entity.Id >= 0 && entity.Id < data.components.Length then
                let comp = data.components.[entity.Id]
                if not <| obj.ReferenceEquals (comp, null) then
                    data.components.[entity.Id] <- null
                    this.DeferComponentEvent <| fun () -> publishComponentRemoved entity comp t
                    this.DeferDispose comp
                    Some comp  
                else None  
            else
                None

    member this.AddComponents (entity: Entity) (comps: obj list) =
        comps
        |> List.iter (fun comp ->
            this.AddComponent (entity, comp, comp.GetType ())
        )

    member this.RemoveAllComponents (entity: Entity) =
        lookup.Keys
        |> Seq.iter (fun key ->
            this.TryRemoveComponent (entity, key) |> ignore
        )

    member this.IterateInternal<'T> (f: Entity * 'T -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T> with
        | (false,_) -> ()
        | (_,data) ->

            let count = data.entities.Count

            let inline iter i =
                let entity = data.entities.[i]
                let com = data.components.[entity.Id]

                if
                    not <| obj.ReferenceEquals (com, null) &&
                    predicate entity.Id
                    then
                    f (entity, (com :?> 'T))

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i

    member this.IterateInternal<'T1, 'T2> (f: Entity * 'T1 * 'T2 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2> with
        | (false,_),_
        | _,(false,_) -> ()
        | (_,data1),(_,data2) ->

            let count =
                [|data1.entities.Count;data2.entities.Count|] |> Array.minBy id

            let data =
                [|data1;data2|] |> Array.minBy (fun x -> x.entities.Count)

            for i = 0 to count - 1 do
                let entity = data.entities.[i]
                let com1 = data1.components.[entity.Id]
                let com2 = data2.components.[entity.Id]

                if 
                    not <| obj.ReferenceEquals (com1, null) && 
                    not <| obj.ReferenceEquals (com2, null) &&
                    predicate entity.Id
                    then
                    f (entity, (com1 :?> 'T1), (com2 :?> 'T2))

    member this.IterateInternal<'T1, 'T2, 'T3> (f: Entity * 'T1 * 'T2 * 'T3 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2>, lookup.TryGetValue typeof<'T3> with
        | (false,_),_,_
        | _,(false,_),_
        | _,_,(false,_) -> ()
        | (_,data1),(_,data2),(_,data3) ->

            let count =
                [|data1.entities.Count;data2.entities.Count;data3.entities.Count|] |> Array.minBy id

            let data =
                [|data1;data2;data3|] |> Array.minBy (fun x -> x.entities.Count)

            for i = 0 to count - 1 do
                let entity = data.entities.[i]
                let com1 = data1.components.[entity.Id]
                let com2 = data2.components.[entity.Id]
                let com3 = data3.components.[entity.Id]

                if 
                    not <| obj.ReferenceEquals (com1, null) && 
                    not <| obj.ReferenceEquals (com2, null) &&
                    not <| obj.ReferenceEquals (com3, null) &&
                    predicate entity.Id
                    then
                    f (entity, (com1 :?> 'T1), (com2 :?> 'T2), (com3 :?> 'T3))

    member this.IterateInternal<'T1, 'T2, 'T3, 'T4> (f: Entity * 'T1 * 'T2 * 'T3 * 'T4 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2>, lookup.TryGetValue typeof<'T3>, lookup.TryGetValue typeof<'T4> with
        | (false,_),_,_,_
        | _,(false,_),_,_
        | _,_,(false,_),_
        | _,_,_,(false,_) -> ()
        | (_,data1),(_,data2),(_,data3),(_,data4) ->

            let count =
                [|data1.entities.Count;data2.entities.Count;data3.entities.Count;data4.entities.Count|] |> Array.minBy id

            let data =
                [|data1;data2;data3;data4|] |> Array.minBy (fun x -> x.entities.Count)

            for i = 0 to count - 1 do
                let entity = data.entities.[i]
                let com1 = data1.components.[entity.Id]
                let com2 = data2.components.[entity.Id]
                let com3 = data3.components.[entity.Id]
                let com4 = data4.components.[entity.Id]

                if 
                    not <| obj.ReferenceEquals (com1, null) && 
                    not <| obj.ReferenceEquals (com2, null) &&
                    not <| obj.ReferenceEquals (com3, null) &&
                    not <| obj.ReferenceEquals (com4, null) &&
                    predicate entity.Id
                    then
                    f (entity, (com1 :?> 'T1), (com2 :?> 'T2), (com3 :?> 'T3), (com4 :?> 'T4))

    member this.Process () =
        let rec p () =
            if  deferQueue.HasMessages || 
                deferPreEntityEventQueue.HasMessages ||
                deferComponentEventQueue.HasMessages ||
                deferEntityEventQueue.HasMessages ||
                deferDispose.HasMessages
                then

                deferQueue.Process (fun f -> f ())
                deferPreEntityEventQueue.Process eventAggregator.Publish
                deferComponentEventQueue.Process (fun f -> f ())
                deferEntityEventQueue.Process eventAggregator.Publish
                deferDispose.Process (fun x ->
                    x.GetType().GetRuntimeProperties()
                    |> Seq.filter (fun p -> disposableTypeInfo.IsAssignableFrom(p.PropertyType.GetTypeInfo()))
                    |> Seq.iter (fun p -> (p.GetValue(x) :?> IDisposable).Dispose ())
                )
                p ()
            else ()
        p ()

    interface IComponentService with

        member this.Add<'T when 'T :> IComponent> entity (comp: 'T) =
            let inline f () =
                this.AddComponent (entity, comp, typeof<'T>)

            this.Defer f

        member this.Remove<'T when 'T :> IComponent> entity =
            let inline f () =
                this.TryRemoveComponent (entity, typeof<'T>) |> ignore

            this.Defer f

    interface IEntityService with

        member this.Create () = this.Create ()

        member this.Destroy entity =
            let inline f () =
                this.Destroy entity

            this.Defer f

    interface IComponentQuery with

        member this.Has<'T when 'T :> IComponent> (entity: Entity) : bool =
            match lookup.TryGetValue typeof<'T> with
            | false, _ -> false
            | _, data -> 
                if not (entity.Id >= 0 && entity.Id < data.components.Length)
                then false
                else not <| obj.ReferenceEquals (data.components.[entity.Id], null)

        member this.TryGet<'T when 'T :> IComponent> (entity: Entity) : 'T option = 
            match lookup.TryGetValue typeof<'T> with
            | false, _ -> None
            | _, data ->
                if not (entity.Id >= 0 && entity.Id < data.components.Length)
                then None
                else 
                    let comp = data.components.[entity.Id]
                    match obj.ReferenceEquals (comp, null) with
                    | true -> None
                    | _ -> Some (comp :?> 'T)

        member this.TryFind<'T when 'T :> IComponent> (f: (Entity * 'T) -> bool) : (Entity * 'T) option =
            match lookup.TryGetValue typeof<'T> with
            | false, _ -> None
            | _, data -> 
                let count = data.entities.Count

                let rec loop = function
                    | n when n.Equals count -> None
                    | n ->
                        let entity = data.entities.[n]
                        let comp = data.components.[entity.Id]
                        let x = (entity, comp :?> 'T)

                        if f x 
                        then Some x
                        else loop (n + 1)
               
                loop 0

        member this.Get<'T when 'T :> IComponent> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> (result.Add, false, fun _ -> true)

            result.ToArray ()

        member this.Get<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> () : (Entity * 'T1 * 'T2) [] =
            let result = ResizeArray<Entity * 'T1 * 'T2> ()

            this.IterateInternal<'T1, 'T2> (result.Add, false, fun _ -> true)

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