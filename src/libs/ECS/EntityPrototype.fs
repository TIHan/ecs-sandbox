namespace ECS

[<ReferenceEquality>]
type EntityPrototype =
    {
        f: (Entity -> EntityManager -> unit)
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    let create () =
        {
            f = fun _ _ -> ()
        }
     
    let add (f: unit -> #IComponent) prototype =
        { prototype with
            f = fun entity entities -> entities.AddComponent entity (f ()); prototype.f entity entities
        }

    let spawn (entities: EntityManager) prototype =
        entities.Spawn (fun entity ->
            prototype.f entity entities
        )        