namespace ECS

open System.Runtime.CompilerServices

[<Sealed>]
type EntityPrototype

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    val create : unit -> EntityPrototype

    val add : (unit -> #IComponent) -> EntityPrototype -> EntityPrototype

[<Sealed; Extension>]
type EntityManagerExtensions =

    [<Extension>]
    static member Spawn : EntityManager * EntityPrototype -> unit    