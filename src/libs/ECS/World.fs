namespace ECS.Core

open System

open ECS.Core

[<Sealed>]
type ECSWorld<'U> (dependency, entityAmount, systems: ISystem<'U> list) as this =
    let eventAggregator = EventAggregator () :> IEventAggregator
    let entityManager = EntityManager (eventAggregator, entityAmount)
    let componentQuery = entityManager :> IComponentQuery
    let componentService = entityManager :> IComponentService
    let entityService = entityManager :> IEntityService
    let componentQuery = entityManager :> IComponentQuery

    do
        systems
        |> List.iter (fun system -> system.Init this)

    member __.Run () =
        entityManager.Process ()

        systems |> List.iter (fun (sys: ISystem<'U>) ->
            sys.Update this
            entityManager.Process ()
        )

    interface IWorld<'U> with

        member __.Dependency = dependency

        member __.EventAggregator = eventAggregator

        member __.ComponentQuery = componentQuery

        member __.ComponentService = componentService

        member __.EntityService = entityService

module World =

    let inline event (world: IWorld<_>) = world.EventAggregator.GetEvent ()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =
    open World

    let created (world: IWorld<_>) =
        event world
        |> Observable.choose (function
            | Created entity -> Some entity
            | _ -> None
        )

    let spawned (world: IWorld<_>) =
        event world
        |> Observable.choose (function
            | Spawned entity -> Some entity
            | _ -> None
        )

    let destroyed (world: IWorld<_>) =
        event world
        |> Observable.choose (function
            | Destroyed entity -> Some entity
            | _ -> None
        )

module Component =
    open World

    let anyAdded (world: IWorld<_>) =
        event world
        |> Observable.map (fun (AnyComponentAdded x) -> x)

    let anyRemoved (world: IWorld<_>) =
        event world
        |> Observable.map (fun (AnyComponentRemoved x) -> x)

    let added (world: IWorld<'U>) : IObservable<Entity * #IComponent> =
        event world
        |> Observable.map (fun (ComponentAdded x) -> x)

    let removed (world: IWorld<'U>) : IObservable<Entity * #IComponent> =
        event world
        |> Observable.map (fun (ComponentRemoved x) -> x)

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

    let spawn id (world: IWorld<_>) (blueprint: EntityBlueprint) =
        let entity = Entity id

        world.EntityService.Spawn entity
        
        blueprint.componentF
        |> List.iter (fun f -> f entity world.ComponentService)
