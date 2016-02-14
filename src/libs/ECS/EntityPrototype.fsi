namespace ECS

open System.Runtime.CompilerServices

[<Sealed>]
type EntityPrototype

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    val empty : EntityPrototype

    val add : (unit -> #IECSComponent) -> EntityPrototype -> EntityPrototype

[<Sealed; Extension>]
type EntityManagerExtensions =

    [<Extension>]
    static member Spawn : EntityManager * EntityPrototype -> unit    