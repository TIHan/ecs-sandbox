namespace ECS

open System
open System.Diagnostics
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks

type Inbox<'T> () =
    let mutable queue = ConcurrentQueue<'T> ()

    member __.Send msg = queue.Enqueue msg

    member __.TryReceive () =
        match queue.TryDequeue () with
        | false, _ -> None
        | _, msg -> Some msg

type EventAggregator =
    {
        lookup: Dictionary<Type, obj>
    }

    member this.Handle<'T> () : IObservable<'T> =
        let t = typeof<'T>

        match this.lookup.TryGetValue t with
        | false, _ ->
            let event = Event<'T> ()
            this.lookup.Add (t, event)
            event.Publish :> IObservable<'T>
        | _, event ->
            let event = event :?> Event<'T> 
            event.Publish :> IObservable<'T>

    member this.Raise<'T> (eventValue: 'T) : unit =
        let t = typeof<'T>

        let event =
            match this.lookup.TryGetValue t with
            | false, _ ->
                let event = Event<'T> ()
                this.lookup.Add (t, event)
                event
            | _, event -> event :?> Event<'T>

        event.Trigger eventValue

[<Struct>]
type Entity =

    val Id : int

    new id = { Id = id }

[<Sealed>]
type ComponentObjRef (get: unit -> obj, isNull: unit -> bool) =

    member this.Value = get ()

    member this.IsNull = isNull ()

[<Sealed>]
type ComponentRef<'T> (get: unit -> 'T, isNull: unit -> bool) =

    member this.Value = get ()

    member this.IsNull = isNull ()

type IEntityQueryContext =

    abstract HasEntityComponent<'T> : Entity -> bool

    abstract TryGetEntityComponent<'T> : Entity -> 'T option

    abstract GetComponentObjRef<'T> : Entity -> ComponentObjRef

    abstract GetComponentRef<'T> : Entity -> ComponentRef<'T>

    abstract IsActive : Entity -> bool

    abstract ForEachActiveEntity : (Entity -> unit) -> unit


    abstract GetActiveEntityComponents<'T> : unit -> (Entity * 'T) []

    abstract GetActiveEntityComponents<'T1, 'T2> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetInactiveEntityComponents<'T> : unit -> (Entity * 'T) []

    abstract GetInactiveEntityComponents<'T1, 'T2> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetEntityComponents<'T> : unit -> (Entity * 'T) []

    abstract GetEntityComponents<'T1, 'T2> : unit -> (Entity * 'T1 * 'T2) []


    abstract ForEachActiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ForEachActiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ForEachActiveEntityComponent<'T1, 'T2, 'T3> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit


    abstract ForEachInactiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ForEachInactiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ForEachEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ForEachEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit

    
    abstract ParallelForEachActiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachActiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachInactiveEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachInactiveEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachEntityComponent<'T> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachEntityComponent<'T1, 'T2> : (Entity * 'T1 * 'T2 -> unit) -> unit

type EntityLookupData =
    {
        Entities: Entity ResizeArray
        EntitySet: Entity HashSet
        Components: obj []
    }

type EntityManager =
    {
        entityAmount: int
        entitySet: Entity HashSet
        activeEntities: bool []
        lookup: Dictionary<Type, EntityLookupData>
        lockObj: obj 
    }

    static member Create (entityAmount: int) =
        {
            entityAmount = entityAmount
            entitySet = HashSet ()
            activeEntities = Array.init entityAmount (fun _ -> false)
            lookup = Dictionary ()
            lockObj = obj ()
        }

    member this.CreateInactiveEntity id : Entity =
        Entity id

    member this.CreateActiveEntity id : Entity =
        let entity = Entity id
        this.activeEntities.[id] <- true
        entity

    member this.ActivateEntity (entity: Entity) : unit =
        this.activeEntities.[entity.Id] <- true

    member this.DeactivateEntity (entity: Entity) : unit =
        this.activeEntities.[entity.Id] <- false

    member this.DestroyEntity (entity: Entity) =
        this.lookup.Values
        |> Seq.iter (fun data ->
            data.EntitySet.Remove entity |> ignore
            data.Entities.Remove entity |> ignore
            if entity.Id >= 0 && entity.Id < data.Components.Length then
                data.Components.[entity.Id] <- null
        )

        this.activeEntities.[entity.Id] <- false

    member this.LoadComponent<'T> () =
        lock this.lockObj <| fun () ->
            let t = typeof<'T>
            match this.lookup.TryGetValue t with
            | false, _ ->
                let entities = ResizeArray ()
                let entitySet = HashSet ()
                let components = Array.init this.entityAmount (fun _ -> null)
                let data =
                    {
                        Entities = entities
                        EntitySet = entitySet
                        Components = components
                    }

                this.lookup.[t] <- data
            | _ -> ()

    member this.SetEntityComponent<'T> (entity: Entity) (value: 'T) = 
        this.LoadComponent<'T> ()

        let data = this.lookup.[typeof<'T>]

        if data.EntitySet.Add entity then
            data.Entities.Add entity

        data.Components.[entity.Id] <- (value :> obj)

    member this.RemoveEntityComponent<'T> (entity: Entity) =
        let t = typeof<'T>

        match this.lookup.TryGetValue t with
        | false, _ -> ()
        | _, data ->
            data.EntitySet.Remove entity |> ignore
            data.Entities.Remove entity |> ignore
            if entity.Id >= 0 && entity.Id < data.Components.Length then
                data.Components.[entity.Id] <- null

    member inline this.IterateInternal<'T> (f: Entity * 'T -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match this.lookup.TryGetValue typeof<'T> with
        | (false,_) -> ()
        | (_,data) ->

            let count = data.Entities.Count

            let inline iter i =
                let entity = data.Entities.[i]
                let com = data.Components.[entity.Id]

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

    member inline this.IterateInternal<'T1, 'T2> (f: Entity * 'T1 * 'T2 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match this.lookup.TryGetValue typeof<'T1>, this.lookup.TryGetValue typeof<'T2> with
        | (false,_),_
        | _,(false,_) -> ()
        | (_,data1),(_,data2) ->

            let count =
                [|data1.Entities.Count;data2.Entities.Count|] |> Array.minBy id

            let data =
                [|data1;data2|] |> Array.minBy (fun x -> x.Entities.Count)

            for i = 0 to count - 1 do
                let entity = data.Entities.[i]
                let com1 = data1.Components.[entity.Id]
                let com2 = data2.Components.[entity.Id]

                if 
                    not <| obj.ReferenceEquals (com1, null) && 
                    not <| obj.ReferenceEquals (com2, null) &&
                    predicate entity.Id
                    then
                    f (entity, (com1 :?> 'T1), (com2 :?> 'T2))

    member inline this.IterateInternal<'T1, 'T2, 'T3> (f: Entity * 'T1 * 'T2 * 'T3 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match this.lookup.TryGetValue typeof<'T1>, this.lookup.TryGetValue typeof<'T2>, this.lookup.TryGetValue typeof<'T3> with
        | (false,_),_,_
        | _,(false,_),_
        | _,_,(false,_) -> ()
        | (_,data1),(_,data2),(_,data3) ->

            let count =
                [|data1.Entities.Count;data2.Entities.Count;data3.Entities.Count|] |> Array.minBy id

            let data =
                [|data1;data2;data3|] |> Array.minBy (fun x -> x.Entities.Count)

            for i = 0 to count - 1 do
                let entity = data.Entities.[i]
                let com1 = data1.Components.[entity.Id]
                let com2 = data2.Components.[entity.Id]
                let com3 = data3.Components.[entity.Id]

                if 
                    not <| obj.ReferenceEquals (com1, null) && 
                    not <| obj.ReferenceEquals (com2, null) &&
                    not <| obj.ReferenceEquals (com3, null) &&
                    predicate entity.Id
                    then
                    f (entity, (com1 :?> 'T1), (com2 :?> 'T2), (com3 :?> 'T3))

    interface IEntityQueryContext with

        member this.HasEntityComponent<'T> (entity: Entity) : bool =
            match this.lookup.TryGetValue typeof<'T> with
            | false, _ -> false
            | _, data -> 
                if not (entity.Id >= 0 && entity.Id < data.Components.Length)
                then false
                else not <| obj.ReferenceEquals (data.Components.[entity.Id], null)

        member this.TryGetEntityComponent<'T> (entity: Entity) : 'T option = 
            match this.lookup.TryGetValue typeof<'T> with
            | false, _ -> None
            | _, data ->
                if not (entity.Id >= 0 && entity.Id < data.Components.Length)
                then None
                else 
                    match data.Components.[entity.Id] with
                    | null -> None
                    | v -> Some (v :?> 'T)

        member this.GetComponentObjRef<'T> (entity: Entity) : ComponentObjRef =
            this.LoadComponent<'T> ()

            let components = this.lookup.[typeof<'T>].Components
            ComponentObjRef (
                (fun () -> components.[entity.Id]),
                (fun () -> obj.ReferenceEquals (components.[entity.Id], null))
            )

        member this.GetComponentRef<'T> (entity: Entity) =
            this.LoadComponent<'T> ()

            let components = this.lookup.[typeof<'T>].Components
            ComponentRef<'T> (
                (fun () -> components.[entity.Id] :?> 'T),
                (fun () -> obj.ReferenceEquals (components.[entity.Id], null))
            )

        member this.IsActive (entity: Entity) =
            this.activeEntities.[entity.Id]

        member this.ForEachActiveEntity f =
            this.activeEntities
            |> Array.iteri (fun i isActive ->
                match isActive with
                | false -> ()
                | _ -> f (Entity i)
            )


        member this.GetActiveEntityComponents<'T> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> (result.Add, false, fun i -> this.activeEntities.[i])

            result.ToArray ()

        member this.GetActiveEntityComponents<'T1, 'T2> () : (Entity * 'T1 * 'T2) [] =
            let result = ResizeArray<Entity * 'T1 * 'T2> ()

            this.IterateInternal<'T1, 'T2> (result.Add, false, fun i -> this.activeEntities.[i])

            result.ToArray ()


        member this.GetInactiveEntityComponents<'T> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> (result.Add, false, fun i -> not this.activeEntities.[i])

            result.ToArray ()

        member this.GetInactiveEntityComponents<'T1, 'T2> () : (Entity * 'T1 * 'T2) [] =
            let result = ResizeArray<Entity * 'T1 * 'T2> ()

            this.IterateInternal<'T1, 'T2> (result.Add, false, fun i -> not this.activeEntities.[i])

            result.ToArray ()


        member this.GetEntityComponents<'T> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> (result.Add, false, fun _ -> true)

            result.ToArray ()

        member this.GetEntityComponents<'T1, 'T2> () : (Entity * 'T1 * 'T2) [] =
            let result = ResizeArray<Entity * 'T1 * 'T2> ()

            this.IterateInternal<'T1, 'T2> (result.Add, false, fun i -> true)

            result.ToArray ()


        member this.ForEachActiveEntityComponent<'T> f : unit =
            this.IterateInternal<'T> (f, false, fun i -> this.activeEntities.[i])

        member this.ForEachActiveEntityComponent<'T1, 'T2> f : unit =
            this.IterateInternal<'T1, 'T2> (f, false, fun i -> this.activeEntities.[i])

        member this.ForEachActiveEntityComponent<'T1, 'T2, 'T3> f : unit =
            this.IterateInternal<'T1, 'T2, 'T3> (f, false, fun i -> this.activeEntities.[i])


        member this.ForEachInactiveEntityComponent<'T> f : unit =
            this.IterateInternal<'T> (f, false, fun i -> not this.activeEntities.[i])

        member this.ForEachInactiveEntityComponent<'T1, 'T2> f : unit =
            this.IterateInternal<'T1, 'T2> (f, false, fun i -> not this.activeEntities.[i])


        member this.ForEachEntityComponent<'T> f : unit =
            this.IterateInternal<'T> (f, false, fun _ -> true)

        member this.ForEachEntityComponent<'T1, 'T2> f : unit =
            this.IterateInternal<'T1, 'T2> (f, false, fun _ -> true)


        member this.ParallelForEachActiveEntityComponent<'T> f : unit =
            this.IterateInternal<'T> (f, true, fun i -> this.activeEntities.[i])

        member this.ParallelForEachActiveEntityComponent<'T1, 'T2> f : unit =
            this.IterateInternal<'T1, 'T2> (f, true, fun i -> this.activeEntities.[i])


        member this.ParallelForEachInactiveEntityComponent<'T> f : unit =
            this.IterateInternal<'T> (f, true, fun i -> not this.activeEntities.[i])

        member this.ParallelForEachInactiveEntityComponent<'T1, 'T2> f : unit =
            this.IterateInternal<'T1, 'T2> (f, true, fun i -> not this.activeEntities.[i])


        member this.ParallelForEachEntityComponent<'T> f : unit =
            this.IterateInternal<'T> (f, true, fun _ -> true)

        member this.ParallelForEachEntityComponent<'T1, 'T2> f : unit =
            this.IterateInternal<'T1, 'T2> (f, true, fun _ -> true)

type WorldMessage =
    | Execute of (unit -> unit)

[<Sealed>]
type World (entityAmount) =
    let entityManager = EntityManager.Create (entityAmount)
    let eventAggregator : EventAggregator = { lookup = Dictionary () }
    let systems = ResizeArray ()
    let inbox = Inbox ()
    let eventInbox = Inbox ()
    let systemInbox = Inbox ()

    member inline this.Defer f =
        inbox.Send (Execute f)

    member inline this.DeferEvent f =
        eventInbox.Send (Execute f)

    member inline this.DeferSystem f =
        systemInbox.Send (Execute f)

    member val Time = TimeSpan.Zero with get, set

    member val Interval = TimeSpan.Zero with get, set

    member val Delta = 0.f with get, set

    member this.Run () =
        let rec processMessages (inbox: Inbox<WorldMessage>) =
            match inbox.TryReceive () with
            | None -> ()
            | Some msg ->
                match msg with
                | Execute f -> 
                    f ()
                    processMessages inbox

        processMessages systemInbox
        processMessages inbox
        processMessages eventInbox

        systems.ForEach (fun (sys: ISystem) ->
            sys.Update this

            processMessages eventInbox
        )

    member this.Query = entityManager :> IEntityQueryContext

    member this.CreateActiveEntity id (f: Entity -> unit) : unit =
        let inline f () =
            f (entityManager.CreateActiveEntity id)

        this.Defer f

    member this.DestroyEntity (entity: Entity) : unit =
        let inline f () = 
            entityManager.DestroyEntity entity

        this.Defer f

    member this.SetEntityComponent<'T> (value: 'T) (entity: Entity) : unit =
        let inline f () =
            entityManager.SetEntityComponent<'T> entity value

        this.Defer f

    member this.RemoveEntityComponent<'T> (entity: Entity) : unit =
        let inline f () =
            entityManager.RemoveEntityComponent<'T> entity

        this.Defer f

    member this.AddSystem (system: ISystem) : unit =
        let inline f () =
            systems.Add system
            system.Init this

        this.DeferSystem f

    member this.HandleEvent<'T> (f: IObservable<'T> -> unit) : unit =
        let inline f () =
            f <| eventAggregator.Handle ()

        this.DeferEvent f

    member this.RaiseEvent<'T> (eventValue: 'T) : unit =
        let inline f () =
            eventAggregator.Raise eventValue

        this.DeferEvent f


and ISystem =

    abstract Init : World -> unit

    abstract Update : World -> unit
