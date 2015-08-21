namespace ECS.Core

[<Struct>]
type Entity =

    val Id : int

    internal new : int -> Entity

