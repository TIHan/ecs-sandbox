namespace ECS.Core.World

open System

open ECS.Core

[<Sealed>]
type ECSWorld (entityAmount, systems: ISystem list) as this =
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

        systems |> List.iter (fun (sys: ISystem) ->
            sys.Update this
        )

    interface IWorld with

        member __.EventAggregator = eventAggregator

        member __.ComponentQuery = componentQuery

        member __.ComponentService = componentService

        member __.EntityService = entityService

module World =

    let event (world: IWorld) = world.EventAggregator.GetEvent ()

module Entity =
    open World

    let created (world: IWorld) =
        event world
        |> Observable.choose (function
            | Created entity -> Some entity
            | _ -> None
        )

    let spawned (world: IWorld) =
        event world
        |> Observable.choose (function
            | Spawned entity -> Some entity
            | _ -> None
        )

    let destroyed (world: IWorld) =
        event world
        |> Observable.choose (function
            | Destroyed entity -> Some entity
            | _ -> None
        )

module Component =
    open World

    let anyAdded (world: IWorld) =
        event world
        |> Observable.map (fun (AnyComponentAdded x) -> x)

    let anyRemoved (world: IWorld) =
        event world
        |> Observable.map (fun (AnyComponentRemoved x) -> x)

    let added<'T when 'T :> IComponent> (world: IWorld) : IObservable<Entity * 'T> =
        event world
        |> Observable.map (fun (ComponentAdded x) -> x)

    let removed<'T when 'T :> IComponent> (world: IWorld) : IObservable<Entity * 'T> =
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

    let spawn id (world: IWorld) (blueprint: EntityBlueprint) =
        let entity = Entity id

        world.EntityService.Spawn entity
        
        blueprint.componentF
        |> List.iter (fun f -> f entity world.ComponentService)
