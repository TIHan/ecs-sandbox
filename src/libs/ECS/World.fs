namespace ECS.Core

open System
open System.Diagnostics
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading.Tasks

[<Sealed>]
type WorldTime () =

    member val Current = Var.create TimeSpan.Zero with get

    member val Interval = Var.create TimeSpan.Zero with get

    member val Delta = Var.create 0.f with get

type ISystem =

    abstract Init : IWorld -> unit

    abstract Update : IWorld -> unit

and IWorld =

    abstract Time : WorldTime

    abstract EventAggregator : IEventAggregator

    abstract ComponentQuery : IComponentQuery

    abstract ComponentService : IComponentService

    abstract EntityService : IEntityService

[<Sealed>]
type World (entityAmount, systems: ISystem list) as this =
    let eventAggregator = EventAggregator () :> IEventAggregator
    let entityManager = EntityManager (eventAggregator, entityAmount)
    let componentQuery = entityManager :> IComponentQuery
    let componentService = entityManager :> IComponentService
    let entityService = entityManager :> IEntityService
    let componentQuery = entityManager :> IComponentQuery
    let time = WorldTime ()

    do
        systems
        |> List.iter (fun system -> system.Init this)

    member __.Run () =
        entityManager.Process ()

        systems |> List.iter (fun (sys: ISystem) ->
            sys.Update this
            entityManager.Process ()
        )

    interface IWorld with

        member __.Time = time

        member __.EventAggregator = eventAggregator

        member __.ComponentQuery = componentQuery

        member __.ComponentService = componentService

        member __.EntityService = entityService

type World<'T> = World of (IWorld -> 'T) with

    static member (>>=) (World f: World<IObservable<Entity * 'a>>, g: Entity -> 'a -> World<unit>) : World<unit> =
        World (
            fun world ->
                (f world)
                |> Observable.add (fun (ent, t) ->
                    match g ent t with
                    | World f2 -> f2 world
                )
        )

    static member (>>=) (World (f): World<IObservable<'a>>, g: 'a -> World<unit>) : World<unit> =
        World (
            fun world ->
                (f world)
                |> Observable.add (fun t ->
                    match g t with
                    | World f2 -> f2 world
                )
        )

    static member (>>=) (World (f): World<Entity * 'a>, g: Entity -> 'a -> World<'b>) : World<'b> =
        World (
            fun world ->
                let ent, com = f world
                match g ent com with
                | World f2 -> f2 world
        )

    static member (>>=) (World (f): World<'a>, g: 'a -> World<'b>) : World<'b> =
        World (
            fun world ->
                match g (f world) with
                | World f2 -> f2 world
        )

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module World =

    let event<'T when 'T :> IEvent> (world: IWorld) =
        world.EventAggregator.GetEvent<'T> ()

    let entityCreated (world: IWorld) =
        event<EntityEvent> world
        |> Observable.choose (function
            | Created entity -> Some entity
            | _ -> None
        )

    let entitySpawned (world: IWorld) =
        event<EntityEvent> world
        |> Observable.choose (function
            | Spawned entity -> Some entity
            | _ -> None
        )

    let entityDestroyed (world: IWorld) =
        event<EntityEvent> world
        |> Observable.choose (function
            | Destroyed entity -> Some entity
            | _ -> None
        )

    let anyComponentAdded (world: IWorld) =
        event<ComponentEvent> world
        |> Observable.choose (function
            | AnyAdded (entity, comp, t) -> Some (entity, comp, t)
            | _ -> None
        )

    let anyComponentRemoved (world: IWorld) =
        event<ComponentEvent> world
        |> Observable.choose (function
            | AnyRemoved (entity, comp, t) -> Some (entity, comp, t)
            | _ -> None
        )

    let componentAdded<'T when 'T :> IComponent> (world: IWorld) =
        event<ComponentEvent<'T>> world
        |> Observable.choose (function
            | Added (entity, comp) -> Some (entity, comp)
            | _ -> None
        )

    let componentRemoved<'T when 'T :> IComponent> (world: IWorld) =
        event<ComponentEvent<'T>> world
        |> Observable.choose (function
            | Removed (entity, comp) -> Some (entity, comp)
            | _ -> None
        )

    let addComponent<'T when 'T :> IComponent> entity comp (world: IWorld) =
        world.ComponentService.Add<'T> entity comp

    let removeComponent<'T when 'T :> IComponent> entity (world: IWorld) =
        world.ComponentService.Remove<'T> entity

module SafeWorld =

    let endWorld = World (fun _ -> ())

    let event<'T when 'T :> IEvent> : World<IObservable<'T>> =
        World World.event<'T>

    let forEvery<'T when 'T :> IComponent> f =
        World (fun world -> world.ComponentQuery.ForEach<'T> f)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =

    let spawned = World World.entitySpawned

    let destroyed = World World.entityDestroyed

    let anyComponentAdded = World World.anyComponentAdded

    let anyComponentRemoved = World World.anyComponentRemoved

    let componentAdded = World World.componentAdded

    let componentRemoved = World World.componentRemoved

    let addComponent com ent = 
        World.addComponent ent com
        |> World

    let removeComponent ent =
        World.removeComponent ent
        |> World

type ISafeSystem =

    abstract Init : World<unit> list

type EmptyComponent = class end with

    interface IComponent

open SafeWorld

type EmptySafeSystem () =

    interface ISafeSystem with

        member __.Init = [

            Entity.componentAdded >>= fun ent (com: EmptyComponent) -> 
                forEvery<EmptyComponent> (fun _ _ -> ()) >>= fun () ->
                    endWorld

        ]

type EntityBlueprint =
    {
        componentF: (Entity -> IComponentService -> unit) list
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityBlueprint =

    let create () =
        {
            componentF = []
        }
     
    let add<'T when 'T :> IComponent> (compf: unit -> 'T) (blueprint: EntityBlueprint) : EntityBlueprint =
        { blueprint with
            componentF = (fun entity (service: IComponentService) -> service.Add<'T> entity (compf ())) :: blueprint.componentF
        }

    let remove<'T when 'T :> IComponent> (blueprint: EntityBlueprint) : EntityBlueprint =
        { blueprint with
            componentF = (fun entity (service: IComponentService) -> service.Remove<'T> entity) :: blueprint.componentF
        }

    let spawn id (world: IWorld) (blueprint: EntityBlueprint) =
        let entity = Entity id

        world.EntityService.Spawn entity
        
        blueprint.componentF
        |> List.iter (fun f -> f entity world.ComponentService)

        let e = Event<Entity> ()
        let sub = ref Unchecked.defaultof<IDisposable>
        sub := 
            World.entitySpawned world
            |> Observable.filter((=)entity)
            |> Observable.subscribe (fun entity ->
                (!sub).Dispose ()
                e.Trigger entity
            )
        e.Publish