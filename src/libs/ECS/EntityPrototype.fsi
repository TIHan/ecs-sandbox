namespace ECS.Core

[<Sealed>]
type EntityPrototype

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    val create : unit -> EntityPrototype

    val add : (unit -> #IComponent) -> EntityPrototype -> EntityPrototype

    val spawn : EntityManager -> EntityPrototype -> unit
