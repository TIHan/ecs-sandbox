namespace ECS.Core

[<Struct>]
type Entity =

    val Id : int

    new (id) = { Id = id }
