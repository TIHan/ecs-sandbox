namespace ECS

[<AbstractClass>]
type HandleEvent =

    abstract internal Handle : Entities -> Events -> unit

type HandleEvent<'T when 'T :> IECSEvent> =
    inherit HandleEvent

    new : (Entities -> 'T -> unit) -> HandleEvent<'T>

type IECSSystem =

    abstract HandleEvents : HandleEvent list

    abstract Update : Entities -> Events -> unit

type IECSSystem<'D1> =
   
    abstract HandleEvents : HandleEvent list

    abstract Update : Entities -> Events -> 'D1 -> unit

[<RequireQualifiedAccess>]
module Systems =

    [<Sealed>]
    type System =

        interface IECSSystem

        new : string * HandleEvent list * (Entities -> Events -> unit) -> System

    [<Sealed>]
    type System<'D1> =

        interface IECSSystem<'D1>

        new : string * HandleEvent list * (Entities -> Events -> 'D1 -> unit) -> System<'D1>

    [<Sealed>]
    type EventQueue<'T when 'T :> IECSEvent> =

        interface IECSSystem

        new : (Entities -> 'T -> unit) -> EventQueue<'T>

    [<Sealed>]
    type EventQueue<'T, 'D1 when 'T :> IECSEvent> =

        interface IECSSystem<'D1>

        new : (Entities -> 'D1 -> 'T -> unit) -> EventQueue<'T, 'D1>

    [<Sealed>]
    type EntityProcessor =

        interface IECSSystem

        new : unit -> EntityProcessor
