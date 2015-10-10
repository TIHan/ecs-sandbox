namespace ECS.Core

open System
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks

[<AllowNullLiteral>]
type IEntityLookupData =

    abstract TryGetComponent : Entity -> IComponent option

type EntityLookupData<'T> =
    {
        entities: Entity ResizeArray
        entitySet: Entity HashSet
        components: 'T []
    }

    interface IEntityLookupData with

        member this.TryGetComponent entity =
            let id = entity.Id
            if obj.ReferenceEquals (this.components.[id], null) then None
            else Some (this.components.[id] :> obj :?> IComponent)

[<Sealed>]
type EntityManager (entityAmount) =
    let disposableTypeInfo = typeof<IDisposable>.GetTypeInfo()

    let entitySet = HashSet<Entity> ()
    let entityRemovals : ((unit -> unit) ResizeArray) [] = Array.init entityAmount (fun _ -> ResizeArray ())
    let lookup = Dictionary<Type, IEntityLookupData> ()

    let componentActionQueue = ConcurrentQueue<unit -> unit> ()
    let entityActionQueue = ConcurrentQueue<unit -> unit> ()

    let entitySpawnedEvent = Event<Entity> ()
    let entityDestroyedEvent = Event<Entity> ()
    let anyComponentAddedEvent = Event<Entity * IComponent * Type> ()
    let anyComponentRemovedEvent = Event<Entity * IComponent * Type> ()
    let componentAddedEventLookup = ConcurrentDictionary<Type, obj> ()
    let componentRemovedEventLookup = ConcurrentDictionary<Type, obj> ()

    member inline this.DeferComponentAction f =
        componentActionQueue.Enqueue f

    member inline this.DeferEntityAction x =
        entityActionQueue.Enqueue x

    member this.GetEntityLookupData<'T> () : EntityLookupData<'T> =
        let t = typeof<'T>
        let mutable data = null
        if not <| lookup.TryGetValue (t, &data) then
            let entities = ResizeArray (entityAmount)
            let entitySet = HashSet ()
            let components = Array.init<'T> entityAmount (fun _ -> Unchecked.defaultof<'T>)
            
            let data =
                {
                    entities = entities
                    entitySet = entitySet
                    components = components
                }

            lookup.[t] <- data
            data
        else
            data :?> EntityLookupData<'T>

    member this.Spawn entity =
        if entitySet.Contains entity then
            failwithf "Entity #%i already spawned." entity.Id
        else
            this.DeferEntityAction <| fun () -> entitySpawnedEvent.Trigger entity
            entitySet.Add entity |> ignore

    member this.Destroy (entity: Entity) =
        if entitySet.Remove entity then
            let removals = entityRemovals.[entity.Id]
            removals.ForEach (fun f -> f ())
            removals.Clear ()            

    member this.AddComponent<'T when 'T :> IComponent> (entity: Entity, comp: 'T) =
        let t = typeof<'T>

        if entitySet.Contains entity then
            failwithf "Entity #%i has already spawned, cannot add component, %s." entity.Id t.Name

        let data = this.GetEntityLookupData<'T> ()

        if data.entitySet.Add entity then
            entityRemovals.[entity.Id].Add (fun () -> this.TryRemoveComponent<'T> entity |> ignore)
            data.entities.Add entity

            // Setup events
            this.DeferComponentAction <| fun () -> 
                // Any Component Added
                anyComponentAddedEvent.Trigger ((entity, comp :> IComponent, t))

                // Component Added
                let mutable value = Unchecked.defaultof<obj>
                if componentAddedEventLookup.TryGetValue (typeof<'T>, &value) then
                    (value :?> Event<Entity * 'T>).Trigger ((entity, comp))

            data.components.[entity.Id] <- comp
        else
            failwithf "Component %s already added to Entity #%i." t.Name entity.Id
        
    member this.TryRemoveComponent<'T when 'T :> IComponent> (entity: Entity) : 'T option =
        let t = typeof<'T>
        let mutable data = null
        if lookup.TryGetValue (t, &data) then
            let data = data :?> EntityLookupData<'T>

            data.entitySet.Remove entity |> ignore
            data.entities.Remove entity |> ignore

            if entity.Id >= 0 && entity.Id < data.components.Length then
                let comp = data.components.[entity.Id]
                if not <| obj.ReferenceEquals (comp, null) then
                    data.components.[entity.Id] <- Unchecked.defaultof<'T>

                    // Setup events
                    this.DeferComponentAction <| fun () ->
                        // Any Component Removed 
                        anyComponentRemovedEvent.Trigger ((entity, comp :> IComponent, t))

                        // Component Removed
                        let mutable value = Unchecked.defaultof<obj>
                        if componentRemovedEventLookup.TryGetValue (typeof<'T>, &value) then
                            (value :?> Event<Entity * 'T>).Trigger ((entity, comp))

                    Some comp  
                else None  
            else
                None
        else
            None

    member this.TryGet<'T> (entity: Entity, c: byref<'T>) = 
        let mutable data = null
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            if (entity.Id >= 0 && entity.Id < data.components.Length) then
                c <- data.components.[entity.Id]

    member this.TryFind<'T> (f: Entity -> 'T -> bool, result: byref<Entity * 'T>) =
        let mutable data = null
        if lookup.TryGetValue (typeof<'T>, &data) then
            let data = data :?> EntityLookupData<'T>
            let count = data.entities.Count

            let mutable n = 0
            while not (n.Equals count) do    
                let entity = data.entities.[n]
                let comp = data.components.[entity.Id]

                if f entity comp then result <- (entity, comp)
                n <- n + 1  

    member inline this.IterateInternal<'T> (f: Entity -> 'T -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T> with
        | (false,_) -> ()
        | (_,data) ->
            let data = data :?> EntityLookupData<'T>

            let count = data.entities.Count

            let inline iter i =
                let entity = data.entities.[i]
                let com = data.components.[entity.Id]

                if
                    not <| obj.ReferenceEquals (com, null) &&
                    predicate entity.Id
                    then
                    f entity com

            if useParallelism
            then Parallel.For (0, count, iter) |> ignore
            else
                for i = 0 to count - 1 do
                    iter i

    member inline this.IterateInternal<'T1, 'T2> (f: Entity -> 'T1 -> 'T2 -> unit, useParallelism: bool, predicate: int -> bool) : unit =
        match lookup.TryGetValue typeof<'T1>, lookup.TryGetValue typeof<'T2> with
        | (false,_),_
        | _,(false,_) -> ()
        | (_,data1),(_,data2) ->
            let data1 = data1 :?> EntityLookupData<'T1>
            let data2 = data2 :?> EntityLookupData<'T2>

            let entities =
                [|data1.entities;data2.entities|] |> Array.minBy (fun x -> x.Count)

            for i = 0 to entities.Count - 1 do
                let entity = entities.[i]
                let com1 = data1.components.[entity.Id]
                let com2 = data2.components.[entity.Id]

                if 
                    not <| obj.ReferenceEquals (com1, null) && 
                    not <| obj.ReferenceEquals (com2, null) &&
                    predicate entity.Id
                    then
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
                [|data1.entities;data2.entities;data3.entities|] |> Array.minBy (fun x -> x.Count)

            for i = 0 to entities.Count - 1 do
                let entity = entities.[i]
                let com1 = data1.components.[entity.Id]
                let com2 = data2.components.[entity.Id]
                let com3 = data3.components.[entity.Id]

                if 
                    not <| obj.ReferenceEquals (com1, null) && 
                    not <| obj.ReferenceEquals (com2, null) &&
                    not <| obj.ReferenceEquals (com3, null) &&
                    predicate entity.Id
                    then
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
                [|data1.entities;data2.entities;data3.entities;data4.entities|] |> Array.minBy (fun x -> x.Count)

            for i = 0 to entities.Count - 1 do
                let entity = entities.[i]
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
                    f entity com1 com2 com3 com4

    member this.Process () =
        let rec p () =
            if 
                componentActionQueue.Count > 0 ||
                entityActionQueue.Count > 0
                then

                // Component Action Queue 
                while componentActionQueue.Count > 0 do
                    let mutable msg = Unchecked.defaultof<unit -> unit>
                    componentActionQueue.TryDequeue (&msg) |> ignore
                    msg ()

                // Entity Action Queue 
                while entityActionQueue.Count > 0 do
                    let mutable msg = Unchecked.defaultof<unit -> unit>
                    entityActionQueue.TryDequeue (&msg) |> ignore
                    msg ()

                p ()
        p ()

    interface IComponentService with

        member this.Add<'T when 'T :> IComponent> entity (comp: 'T) =
            let inline f () =
                this.AddComponent<'T> (entity, comp)

            this.DeferComponentAction f

        member this.Remove<'T when 'T :> IComponent> entity =
            let inline f () =
                this.TryRemoveComponent<'T> (entity) |> ignore

            this.DeferComponentAction f

        member this.GetAddedEvent<'T when 'T :> IComponent> () =
            let event = componentAddedEventLookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<Entity * 'T> () :> obj))
            (event :?> Event<Entity * 'T>).Publish :> IObservable<Entity * 'T>

        member this.GetRemovedEvent<'T when 'T :> IComponent> () =
            let event = componentRemovedEventLookup.GetOrAdd (typeof<'T>, valueFactory = (fun _ -> Event<Entity * 'T> () :> obj))
            (event :?> Event<Entity * 'T>).Publish :> IObservable<Entity * 'T>

        member this.GetAnyAddedEvent () = anyComponentAddedEvent.Publish :> IObservable<Entity * IComponent * Type>

        member this.GetAnyRemovedEvent () = anyComponentRemovedEvent.Publish :> IObservable<Entity * IComponent * Type>


    interface IEntityService with

        member this.Spawn entity =             
            let inline f () =
                this.Spawn entity

            this.DeferEntityAction f

        member this.Destroy entity =
            let inline f () =
                this.Destroy entity

            this.DeferEntityAction f

        member this.GetSpawnedEvent () = entitySpawnedEvent.Publish :> IObservable<Entity>

        member this.GetDestroyedEvent () = entityDestroyedEvent.Publish :> IObservable<Entity>

    interface IComponentQuery with

        member this.Has<'T when 'T :> IComponent> (entity: Entity) : bool =
            let mutable c = Unchecked.defaultof<'T>
            this.TryGet<'T> (entity, &c)
            obj.ReferenceEquals (c, null)

        member this.TryGet (entity: Entity, t: Type) : IComponent option =
            let mutable data = null
            if lookup.TryGetValue (t, &data) then data.TryGetComponent entity
            else None

        member this.TryGet (entity, c: byref<#IComponent>) =
            this.TryGet (entity, &c)

        member this.TryGet<'T when 'T :> IComponent> (entity: Entity) : 'T option = 
            let mutable c = Unchecked.defaultof<'T>
            this.TryGet<'T> (entity, &c)

            if obj.ReferenceEquals (c, null) then None
            else Some c

        member this.TryFind<'T when 'T :> IComponent> (f: Entity -> 'T -> bool) : (Entity * 'T) option =
            let mutable result = Unchecked.defaultof<Entity * 'T>
            this.TryFind (f, &result)
            
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
