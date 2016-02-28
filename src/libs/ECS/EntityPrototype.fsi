namespace BeyondGames.Ecs

open System.Runtime.CompilerServices

[<Sealed>]
type EntityPrototype

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module EntityPrototype =

    val empty : EntityPrototype

    val combine : EntityPrototype -> EntityPrototype -> EntityPrototype

    val addComponent<'T when 'T :> IEntityComponent and 'T : not struct> : (unit -> 'T) -> EntityPrototype -> EntityPrototype

[<Sealed; Extension>]
type EntityManagerExtensions =

    [<Extension>]
    static member Spawn : EntityManager * EntityPrototype -> unit    