namespace ECS.Core

[<ReferenceEquality>]
type EntityPrototype =
    {
        funcs: (Entity -> EntityManager -> unit) list
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    let create () =
        {
            funcs = []
        }
     
    let add (f: unit -> #IComponent) prototype =
        { prototype with
            funcs = (fun entity entities -> entities.AddComponent entity (f ())) :: prototype.funcs
        }

    let spawn (entities: EntityManager) prototype =
        entities.Spawn (fun entity ->
            prototype.funcs
            |> List.iter (fun f -> f entity entities)
        )
