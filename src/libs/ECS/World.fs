namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : World -> unit

    abstract Update : World -> unit

and [<Sealed>] World (entityAmount, systems: ISystem list) as this =
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

        systems |> List.iter (fun (sys: ISystem) ->
            sys.Update this
            entityManager.Process ()
        )

    member __.EventAggregator = eventAggregator

    member __.ComponentQuery = componentQuery

    member __.ComponentService = componentService

    member __.EntityService = entityService

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module World =

    let inline event (world: World) = world.EventAggregator.GetEvent ()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =
    open World

    let onSpawned (world: World) = 
        world.EntityService.GetSpawnedEvent ()

    let onDestroyed (world: World) = 
        world.EntityService.GetDestroyedEvent ()

module Component =
    open World

    let onAnyAdded (world: World) = 
        world.ComponentService.GetAnyAddedEvent ()

    let onAnyRemoved (world: World) = 
        world.ComponentService.GetAnyRemovedEvent ()

    let onAdded (world: World) : IObservable<Entity * #IComponent> =
        world.ComponentService.GetAddedEvent ()

    let onRemoved (world: World) : IObservable<Entity * #IComponent> =
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

    let spawn id (world: World) (blueprint: EntityBlueprint) =
        let entity = Entity id

        blueprint.componentF
        |> List.iter (fun f -> f entity world.ComponentService)

        world.EntityService.Spawn entity
