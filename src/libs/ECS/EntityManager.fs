namespace ECS.Core

open System
open System.Reflection
open System.Collections.Generic
open System.Threading.Tasks

type EntityEvent =
    | Created of Entity
    | Spawned of Entity
    | Destroyed of Entity

    interface IEvent

type AnyComponentAdded = AnyComponentAdded of (Entity * IComponent * Type) with

    interface IEvent

type AnyComponentRemoved = AnyComponentRemoved of (Entity * IComponent * Type) with

    interface IEvent

type ComponentAdded<'T> = ComponentAdded of (Entity * 'T) with

    interface IEvent

type ComponentRemoved<'T> = ComponentRemoved of (Entity * 'T) with

    interface IEvent

type IComponentQuery =

    abstract Has<'T when 'T :> IComponent> : Entity -> bool

    abstract TryGet : Entity * Type -> IComponent option

    abstract TryGet<'T when 'T :> IComponent> : Entity -> 'T option

    abstract TryFind<'T when 'T :> IComponent> : (Entity * 'T -> bool) -> (Entity * 'T) option

    abstract Get<'T when 'T :> IComponent> : unit -> (Entity * 'T) []

    abstract Get<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : unit -> (Entity * 'T1 * 'T2) []

    abstract ForEach<'T when 'T :> IComponent> : (Entity -> 'T -> unit) -> unit

    abstract ForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

    abstract ForEach<'T1, 'T2, 'T3, 'T4 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent and 'T4 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> 'T4 -> unit) -> unit

    abstract ParallelForEach<'T when 'T :> IComponent> : (Entity -> 'T -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> : (Entity -> 'T1 -> 'T2 -> unit) -> unit

    abstract ParallelForEach<'T1, 'T2, 'T3 when 'T1 :> IComponent and 'T2 :> IComponent and 'T3 :> IComponent> : (Entity -> 'T1 -> 'T2 -> 'T3 -> unit) -> unit

type IComponentService =

    abstract Add<'T when 'T :> IComponent> : Entity -> 'T -> unit

    abstract Remove<'T when 'T :> IComponent> : Entity -> unit

type IEntityService =

    abstract Spawn : Entity -> unit

    abstract Destroy : Entity -> unit

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
type EntityManager (eventAggregator: IEventAggregator, entityAmount) =
    let disposableTypeInfo = typeof<IDisposable>.GetTypeInfo()

    let entitySet = HashSet<Entity> ()
    let componentSet = HashSet<IComponent> ()
    let entityRemovals : ((unit -> unit) ResizeArray) [] = Array.init entityAmount (fun _ -> ResizeArray ())
    let lookup = Dictionary<Type, IEntityLookupData> ()

    let deferQueue = MessageQueue<unit -> unit> ()
    let deferPreEntityEventQueue = MessageQueue<EntityEvent> ()
    let deferComponentEventQueue = MessageQueue<unit -> unit> ()
    let deferEntityEventQueue = MessageQueue<EntityEvent> ()
    let deferDispose = MessageQueue<obj> ()

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
            this.DeferPreEntityEvent (Created entity)
            this.DeferEntityEvent (Spawned entity)
            entitySet.Add entity |> ignore

    member this.Destroy (entity: Entity) =
        let removals = entityRemovals.[entity.Id]
        removals.ForEach (fun f -> f ())
        removals.Clear ()
        entitySet.Remove entity |> ignore     

    member this.AddComponent<'T when 'T :> IComponent> (entity: Entity, comp: 'T) =
        let t = typeof<'T>

        if not <| entitySet.Contains entity then
            failwithf "Entity #%i has not been spawned." entity.Id

        if not <| componentSet.Add (comp) then
            failwithf "Component %s has already been used." t.Name

        let data = this.GetEntityLookupData<'T> ()

        if data.entitySet.Add entity then
            entityRemovals.[entity.Id].Add (fun () -> this.TryRemoveComponent<'T> entity |> ignore)
            data.entities.Add entity
            this.DeferComponentEvent <| fun () -> 
                eventAggregator.Publish (AnyComponentAdded (entity, comp :> IComponent, t))
                eventAggregator.Publish (ComponentAdded (entity, comp))
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
                    componentSet.Remove comp |> ignore
                    data.components.[entity.Id] <- Unchecked.defaultof<'T>
                    this.DeferComponentEvent <| fun () -> 
                        eventAggregator.Publish (AnyComponentRemoved (entity, comp :> IComponent, t))
                        eventAggregator.Publish (ComponentRemoved (entity, comp))
                    this.DeferDispose comp
                    Some comp  
                else None  
            else
                None
        else
            None

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
                    |> Seq.filter (fun p ->
                        disposableTypeInfo.IsAssignableFrom(p.PropertyType.GetTypeInfo())
                    )
                    |> Seq.iter (fun p -> (p.GetValue(x) :?> IDisposable).Dispose ())
                )
                p ()
            else ()
        p ()

    interface IComponentService with

        member this.Add<'T when 'T :> IComponent> entity (comp: 'T) =
            let inline f () =
                this.AddComponent<'T> (entity, comp)

            this.Defer f

        member this.Remove<'T when 'T :> IComponent> entity =
            let inline f () =
                this.TryRemoveComponent<'T> (entity) |> ignore

            this.Defer f

    interface IEntityService with

        member this.Spawn entity =             
            let inline f () =
                this.Spawn entity

            this.Defer f

        member this.Destroy entity =
            let inline f () =
                this.Destroy entity

            this.Defer f

    interface IComponentQuery with

        member this.Has<'T when 'T :> IComponent> (entity: Entity) : bool =
            let mutable data = null
            if lookup.TryGetValue (typeof<'T>, &data) then
                let data = data :?> EntityLookupData<'T>
                if not (entity.Id >= 0 && entity.Id < data.components.Length)
                then false
                else obj.ReferenceEquals (data.components.[entity.Id], null)
            else
                false

        member this.TryGet (entity: Entity, t: Type) : IComponent option =
            let mutable data = null
            if lookup.TryGetValue (t, &data) then data.TryGetComponent entity
            else None

        member this.TryGet<'T when 'T :> IComponent> (entity: Entity) : 'T option = 
            let mutable data = null
            if lookup.TryGetValue (typeof<'T>, &data) then
                let data = data :?> EntityLookupData<'T>
                if not (entity.Id >= 0 && entity.Id < data.components.Length)
                then None
                else 
                    let comp = data.components.[entity.Id]
                    match obj.ReferenceEquals (comp, null) with
                    | true -> None
                    | _ -> Some comp
            else
                None

        member this.TryFind<'T when 'T :> IComponent> (f: (Entity * 'T) -> bool) : (Entity * 'T) option =
            let mutable data = null
            if lookup.TryGetValue (typeof<'T>, &data) then
                let data = data :?> EntityLookupData<'T>
                let count = data.entities.Count

                let rec loop = function
                    | n when n.Equals count -> None
                    | n ->
                        let entity = data.entities.[n]
                        let comp = data.components.[entity.Id]
                        let x = (entity, comp)

                        if f x 
                        then Some x
                        else loop (n + 1)
               
                loop 0
            else
                None

        member this.Get<'T when 'T :> IComponent> () : (Entity * 'T) [] =
            let result = ResizeArray<Entity * 'T> ()

            this.IterateInternal<'T> ((fun entity x -> result.Add(entity, x)), false, fun _ -> true)

            result.ToArray ()

        member this.Get<'T1, 'T2 when 'T1 :> IComponent and 'T2 :> IComponent> () : (Entity * 'T1 * 'T2) [] =
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
