namespace ECS.Core

open System

open ECS.Core

type ISystem =

    abstract Init : EntityManager * EventAggregator -> unit

    abstract Update : EntityManager * EventAggregator -> unit

type [<Sealed>] World (entityAmount, systems: ISystem list) =
    let eventAggregator = EventAggregator ()
    let entityManager = EntityManager (entityAmount)
    let deps = (entityManager, eventAggregator)

    do
        systems
        |> List.iter (fun system -> system.Init deps)

    member __.Run () =
        entityManager.Process ()

        systems |> List.iter (fun (sys: ISystem) ->
            sys.Update deps
            entityManager.Process ()
        )

    member __.Events = eventAggregator

    member __.Entities = entityManager

type EventSystem<'Event when 'Event :> IEvent> (update) =
    let queue = System.Collections.Concurrent.ConcurrentQueue<'Event> ()

    interface ISystem with

        member __.Init (_, events) =
            events.GetEvent<'Event> ()
            |> Observable.add queue.Enqueue

        member __.Update (entities, _) =
            let iter = update entities
            let mutable eventValue = Unchecked.defaultof<'Event>
            while queue.TryDequeue (&eventValue) do
                iter eventValue

type EntityBlueprint =
    {
        componentF: (Entity -> EntityManager -> unit) list
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityBlueprint =

    let create () =
        {
            componentF = []
        }
     
    let add<'T when 'T :> IComponent> (compf: unit -> 'T) (blueprint: EntityBlueprint) : EntityBlueprint =
        { blueprint with
            componentF = (fun entity entityManager -> entityManager.AddComponent<'T> entity (compf ())) :: blueprint.componentF
        }

    let remove<'T when 'T :> IComponent> (blueprint: EntityBlueprint) : EntityBlueprint =
        { blueprint with
            componentF = (fun entity entityManager -> entityManager.RemoveComponent<'T> entity) :: blueprint.componentF
        }

    let spawn id (entityManager: EntityManager) (blueprint: EntityBlueprint) =
        let entity = Entity id

        blueprint.componentF
        |> List.iter (fun f -> f entity entityManager)

        entityManager.Spawn entity
