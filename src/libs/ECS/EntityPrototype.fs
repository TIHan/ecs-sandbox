namespace BeyondGames.Ecs

open System.Runtime.CompilerServices

[<ReferenceEquality>]
type EntityPrototype =
    {
        addComponents: (Entity -> EntityManager -> unit)
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    let empty =
        {
            addComponents = fun _ _ -> ()
        }

    let combine (p1: EntityPrototype) (p2: EntityPrototype) =
        {
            addComponents = fun entity entities -> p1.addComponents entity entities; p2.addComponents entity entities
        }
     
    let addComponent (f: unit -> #IEntityComponent) prototype =
        { prototype with
            addComponents = fun entity entities -> prototype.addComponents entity entities; entities.AddComponent entity (f ())
        }

[<Sealed; Extension>]
type EntityManagerExtensions private () =

    [<Extension>]
    static member Spawn (entityManager: EntityManager, prototype: EntityPrototype) =
        entityManager.Spawn (fun entity ->
            prototype.addComponents entity entityManager
        )        