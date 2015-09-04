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
