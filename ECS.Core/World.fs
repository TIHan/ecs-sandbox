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

 type EntityDescription =
    {
        id: int
        entity: Entity option
        creationF: IEntityService -> unit
        componentF: (Entity -> IComponentService -> unit) list
    }

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

    let componentAdded<'T when 'T :> IComponent<'T>> (world: IWorld) =
        event<ComponentEvent<'T>> world
        |> Observable.choose (function
            | Added (entity, comp) -> Some (entity, comp)
            | _ -> None
        )

    let componentRemoved<'T when 'T :> IComponent<'T>> (world: IWorld) =
        event<ComponentEvent<'T>> world
        |> Observable.choose (function
            | Removed (entity, comp) -> Some (entity, comp)
            | _ -> None
        )

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Entity =

    let create id =
        {
            id = id
            entity = None
            creationF = fun (service: IEntityService) -> service.Create id
            componentF = []
        }
     
    let add<'T when 'T :> IComponent<'T>> (comp: 'T) (desc: EntityDescription) : EntityDescription =
        { desc with
            componentF = (fun entity (service: IComponentService) -> service.Add<'T> entity comp) :: desc.componentF
        }

    let remove<'T when 'T :> IComponent<'T>> (desc: EntityDescription) : EntityDescription =
        { desc with
            componentF = (fun entity (service: IComponentService) -> service.Remove<'T> entity) :: desc.componentF
        }

    let run (world: IWorld) (desc: EntityDescription) =
        match desc.entity with
        | None ->
            let subscription = ref Unchecked.defaultof<IDisposable>

            desc.creationF world.EntityService

            subscription :=
                World.entityCreated world
                |> Observable.subscribe (function
                    | entity when entity.Id.Equals desc.id ->
                        desc.componentF 
                        |> List.iter (fun f -> f entity world.ComponentService)
                        (!subscription).Dispose ()
                    | _ -> ()
                )
        | Some entity ->
            desc.componentF
            |> List.iter (fun f -> f entity world.ComponentService)