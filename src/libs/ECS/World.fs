namespace ECS.Core

open System

open ECS.Core

[<Sealed>]
type ECSWorld<'U> (dependency, entityAmount, systems: ISystem<'U> list) as this =
    let eventAggregator = EventAggregator () :> IEventAggregator
    let entityManager = EntityManager (entityAmount)
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

    let spawned (world: IWorld<_>) = 
        world.EntityService.GetSpawnedEvent ()

    let destroyed (world: IWorld<_>) = 
        world.EntityService.GetDestroyedEvent ()

module Component =
    open World

    let anyAdded (world: IWorld<_>) = 
        world.ComponentService.GetAnyAddedEvent ()

    let anyRemoved (world: IWorld<_>) = 
        world.ComponentService.GetAnyRemovedEvent ()

    let added (world: IWorld<'U>) : IObservable<Entity * #IComponent> =
        world.ComponentService.GetAddedEvent ()

    let removed (world: IWorld<'U>) : IObservable<Entity * #IComponent> =
        world.ComponentService.GetRemovedEvent ()

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
