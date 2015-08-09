namespace ECS.Core

open System

type IComponent<'T
        when 'T : (new : unit -> 'T)
        and 'T : not struct
    > = interface end
