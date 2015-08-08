namespace ECS.Core

open System

type IComponent = interface end

type IComponent<'T when 'T : (new : unit -> 'T)> =
    inherit IComponent 
