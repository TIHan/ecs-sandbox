namespace ECS

open System.Runtime.CompilerServices

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
            f = fun entity entities -> prototype.f entity entities; entities.AddComponent entity (f ())
        }

[<Sealed; Extension>]
type EntityManagerExtensions private () =

    [<Extension>]
    static member Spawn (entityManager: EntityManager, prototype: EntityPrototype) =
        entityManager.Spawn (fun entity ->
            prototype.f entity entityManager
        )        