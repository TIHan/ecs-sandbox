namespace ECS.Core

open System

type IComponent = interface end

[<Interface>]
type IComponent<'T when 'T : (new : unit -> 'T)> =
    inherit IComponent 