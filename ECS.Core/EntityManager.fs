namespace ECS.Core

open System
open System.Collections.Generic
open System.Threading.Tasks

type EntityEvent =
    | CreatedActive of Entity
    | CreatedInactive of Entity
    | Activated of Entity
    | Deactivated of Entity
    | Destroyed of Entity
    | ComponentAdded of Entity * Type * obj
    | ComponentRemoved of Entity * Type * obj

type IEntityQuery =

    abstract HasComponent<'T when 'T :> IComponent> : Entity -> bool

    abstract TryGetComponent<'T when 'T :> IComponent> : Entity -> 'T option

    abstract IsActive : Entity -> bool

    abstract ForEachActive : (Entity -> unit) -> unit


    abstract GetActiveComponents<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract GetActiveComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetInactiveComponents<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract GetInactiveComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []


    abstract GetComponents<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract GetComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []


    abstract ForEachActiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEachActiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    abstract ForEachActiveComponent<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity * 'T1 * 'T2 * 'T3 -> unit) -> unit


    abstract ForEachInactiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEachInactiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ForEachComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ForEachComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

    
    abstract ParallelForEachActiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachActiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachInactiveComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachInactiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit


    abstract ParallelForEachComponent<'T when 'T :> IComponent> : (Entity * 'T -> unit) -> unit

    abstract ParallelForEachComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity * 'T1 * 'T2 -> unit) -> unit

type IEntityFactory =

    abstract CreateInactive : id: int -> IComponent list -> unit

    abstract CreateActive : id: int -> IComponent list -> unit

    abstract Activate : Entity -> unit

    abstract Deactivate : Entity -> unit

    abstract Destroy : Entity -> unit

    abstract AddComponent<'T when 'T :> IComponent> : Entity -> 'T -> unit

    abstract RemoveComponent<'T when 'T :> IComponent> : Entity -> unit

    abstract Process : unit -> unit

type EntityLookupData =
    {
        entities: Entity ResizeArray
        entitySet: Entity HashSet
        components: IComponent []
    }

[<Sealed>]
type EntityManager (eventAggregator: IEventAggregator, entityAmount) =
    let activeEntities = Array.init entityAmount (fun _ -> false)
    let lookup = Dictionary<Type, EntityLookupData> ()
    let lockObj = obj ()
    let deferQueue = MessageQueue<unit -> unit> ()

    member this.RemoveAllComponents (entity: Entity) =
        lookup
        |> Seq.iter (fun pair ->
            match this.TryRemoveComponent (entity, pair.Key) with
            | Some comp ->
                eventAggregator.Publish (ComponentRemoved (entity, pair.Key, comp))
                comp.Dispose ()
            | _ -> ()
        )

    member this.CreateInactive id : Entity =
        Entity id

    member this.CreateActive id : Entity =
        let entity = Entity id
        activeEntities.[id] <- true
        entity

    member this.Activate (entity: Entity) : unit =
        activeEntities.[entity.Id] <- true

    member this.Deactivate (entity: Entity) : unit =
        activeEntities.[entity.Id] <- false

    member this.Destroy (entity: Entity) =
        lookup.Values
        |> Seq.iter (fun data ->
            data.entitySet.Remove entity |> ignore
            data.entities.Remove entity |> ignore
            if entity.Id >= 0 && entity.Id < data.components.Length then
                data.components.[entity.Id].Dispose ()
                data.components.[entity.Id] <- Unchecked.defaultof<IComponent>
        )

        activeEntities.[entity.Id] <- false

    member this.LoadComponent (t: Type) =
        lock lockObj <| fun () ->
            match lookup.TryGetValue t with
            | false, _ ->
                let entities = ResizeArray ()
                let entitySet = HashSet ()
                let components = Array.init entityAmount (fun _ -> Unchecked.defaultof<IComponent>)
                let data =
                    {
                        entities = entities
                        entitySet = entitySet
                        components = components
                    }

                lookup.[t] <- data
            | _ -> ()        

    member this.AddComponent (entity: Entity, value: 'T, t: Type) =
        this.LoadComponent (t)

        let data = lookup.[t]

        if data.entitySet.Add entity then
            data.entities.Add entity

        data.components.[entity.Id] <- (value :> IComponent)   
        
    member this.TryRemoveComponent (entity: Entity, t: Type) : IComponent option =
        match lookup.TryGetValue t with
        | false, _ -> None
        | _, data ->
            data.entitySet.Remove entity |> ignore
            data.entities.Remove entity |> ignore
            if entity.Id >= 0 && entity.Id < data.components.Length then
                data.components.[entity.Id] <- Unchecked.defaultof<IComponent>
                Some data.components.[entity.Id]     
            else
                None

    member inline this.AddComponent<'T when 'T :> IComponent> (entity: Entity, value: 'T) = 
        this.AddComponent (entity, value, typeof<'T>)

    member inline this.TryRemoveComponent<'T when 'T :> IComponent> (entity: Entity) =
        this.TryRemoveComponent (entity, typeof<'T>)

    member this.IterateInternal<'T when 'T :> IComponent> (f: Entity * 'T -> unit, useParallelism: bool, predicate: int -> bool) : unit =
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

    member this.IterateInternal<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> (f: Entity * 'T1 * 'T2 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
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

    member this.IterateInternal<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> (f: Entity * 'T1 * 'T2 * 'T3 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
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

    member inline this.Defer f =
        deferQueue.Push f

    interface IEntityFactory with

        member this.CreateInactive id comps =
            let inline g () =
                let entity = this.CreateInactive id
                comps 
                |> List.iter (fun comp -> 
                    let t = comp.GetType ()
                    this.AddComponent (entity, comp, t)
                    eventAggregator.Publish (ComponentAdded (entity, t, comp))
                )
                eventAggregator.Publish (CreatedInactive entity)

            this.Defer g

        member this.CreateActive id comps =
            let inline g () =
                let entity = this.CreateActive id
                comps 
                |> List.iter (fun comp -> 
                    let t = comp.GetType ()
                    this.AddComponent (entity, comp, t)
                    eventAggregator.Publish (ComponentAdded (entity, t, comp))
                )
                eventAggregator.Publish (CreatedActive entity)

            this.Defer g

        member this.Activate entity =
            let inline f () =
                this.Activate entity
                eventAggregator.Publish (Activated entity)

            this.Defer f

        member this.Deactivate entity =
            let inline f () =
                this.Deactivate entity
                eventAggregator.Publish (Deactivated entity)

            this.Defer f

        member this.Destroy entity =
            let inline f () =
                this.Destroy entity
                this.RemoveAllComponents entity
                eventAggregator.Publish (Destroyed entity)

            this.Defer f

        member this.AddComponent<'T when 'T :> IComponent> entity comp =
            let inline f () =
                this.AddComponent<'T> (entity, comp)
                eventAggregator.Publish (ComponentAdded (entity, typeof<'T>, comp))

            this.Defer f

        member this.RemoveComponent<'T when 'T :> IComponent> entity =
            let inline f () =
                match this.TryRemoveComponent<'T> (entity) with
                | Some comp ->
                    eventAggregator.Publish (ComponentRemoved (entity, typeof<'T>, comp))
                    comp.Dispose ()
                | _ -> ()

            this.Defer f  
            
        member this.Process () =
            deferQueue.Process (fun f -> f ())      

    interface IEntityQuery with

        member this.HasComponent<'T when 'T :> IComponent> (entity: Entity) : bool =
            match lookup.TryGetValue typeof<'T> with
            | false, _ -> false
            | _, data -> 
                if not (entity.Id >= 0 && entity.Id < data.components.Length)
                then false
                else not <| obj.ReferenceEquals (data.components.[entity.Id], null)

        member this.TryGetComponent<'T when 'T :> IComponent> (entity: Entity) : 'T option = 
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

        member this.IsActive (entity: Entity) =
            activeEntities.[entity.Id]

        member this.ForEachActive f =
            activeEntities
            |> Array.iteri (fun i isActive ->
                match isActive with
                | false -> ()
                | _ -> f (Entity i)
            )


        member this.GetActiveComponents<'T when 'T :> IComponent> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> (result.Add, false, fun i -> activeEntities.[i])

            result.ToArray ()

        member this.GetActiveComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> () : (Entity * 'T1 * 'T2) [] =
            let result = ResizeArray<Entity * 'T1 * 'T2> ()

            this.IterateInternal<'T1, 'T2> (result.Add, false, fun i -> activeEntities.[i])

            result.ToArray ()


        member this.GetInactiveComponents<'T when 'T :> IComponent> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> (result.Add, false, fun i -> not activeEntities.[i])

            result.ToArray ()

        member this.GetInactiveComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> () : (Entity * 'T1 * 'T2) [] =
            let result = ResizeArray<Entity * 'T1 * 'T2> ()

            this.IterateInternal<'T1, 'T2> (result.Add, false, fun i -> not activeEntities.[i])

            result.ToArray ()


        member this.GetComponents<'T when 'T :> IComponent> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> (result.Add, false, fun _ -> true)

            result.ToArray ()

        member this.GetComponents<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> () : (Entity * 'T1 * 'T2) [] =
            let result = ResizeArray<Entity * 'T1 * 'T2> ()

            this.IterateInternal<'T1, 'T2> (result.Add, false, fun i -> true)

            result.ToArray ()


        member this.ForEachActiveComponent<'T when 'T :> IComponent> f : unit =
            this.IterateInternal<'T> (f, false, fun i -> activeEntities.[i])

        member this.ForEachActiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
            this.IterateInternal<'T1, 'T2> (f, false, fun i -> activeEntities.[i])

        member this.ForEachActiveComponent<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> f : unit =
            this.IterateInternal<'T1, 'T2, 'T3> (f, false, fun i -> activeEntities.[i])


        member this.ForEachInactiveComponent<'T when 'T :> IComponent> f : unit =
            this.IterateInternal<'T> (f, false, fun i -> not activeEntities.[i])

        member this.ForEachInactiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
            this.IterateInternal<'T1, 'T2> (f, false, fun i -> not activeEntities.[i])


        member this.ForEachComponent<'T when 'T :> IComponent> f : unit =
            this.IterateInternal<'T> (f, false, fun _ -> true)

        member this.ForEachComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
            this.IterateInternal<'T1, 'T2> (f, false, fun _ -> true)


        member this.ParallelForEachActiveComponent<'T when 'T :> IComponent> f : unit =
            this.IterateInternal<'T> (f, true, fun i -> activeEntities.[i])

        member this.ParallelForEachActiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
            this.IterateInternal<'T1, 'T2> (f, true, fun i -> activeEntities.[i])


        member this.ParallelForEachInactiveComponent<'T when 'T :> IComponent> f : unit =
            this.IterateInternal<'T> (f, true, fun i -> not activeEntities.[i])

        member this.ParallelForEachInactiveComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
            this.IterateInternal<'T1, 'T2> (f, true, fun i -> not activeEntities.[i])


        member this.ParallelForEachComponent<'T when 'T :> IComponent> f : unit =
            this.IterateInternal<'T> (f, true, fun _ -> true)

        member this.ParallelForEachComponent<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> f : unit =
            this.IterateInternal<'T1, 'T2> (f, true, fun _ -> true)
