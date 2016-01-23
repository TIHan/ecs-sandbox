namespace ECS.Core

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
