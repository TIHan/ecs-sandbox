namespace Salty.Core

open System

open ECS.Core

type Salty =
    {
        CurrentTime: Val<TimeSpan>
        DeltaTime: Val<single>
        Interval: Val<TimeSpan>
    }

type SaltyWorld<'T> = World<Salty, 'T>

[<AutoOpen>]
module DSL =

    let DoNothing : World<_, unit> = fun _ -> ()

    let inline worldReturn (a: 'a) : World<_, 'a> = fun _ -> a

    let inline (>>=) (w: World<_, 'a>) (f: 'a -> World<_, 'b>) : World<_, 'b> =
        fun world -> (f (w world)) world

    let inline (>>.) (w1: World<_, 'a>) (w2: World<_, 'b>) =
        fun world ->
            w1 world |> ignore
            w2 world

    let inline skip (x: World<_, _>) : World<_, unit> =
        fun world -> x world |> ignore

    let inline onEvent (w: World<_, IObservable<'a>>) (f: 'a -> World<_, unit>) : World<_, unit> =
        fun world ->
            (w world) 
            |> Observable.add (fun a ->
                (f a) world
            )

    let inline onUpdate (obs: IObservable<'a>) (f: 'a -> World<_, unit>) : World<_, unit> =
        fun world -> obs |> Observable.add (fun x -> (f x) world)

    let inline (==>) (obs: IObservable<'a>) (v: Val<'a>) : World<_, unit> =
        fun world -> v.Assign obs
            
    let inline update (v: Var<'a>) (f: 'a -> 'a) : World<_, unit> =
        fun world -> v.Value <- f v.Value 

    let inline rule (f: Entity -> 'T -> World<_, unit>) : World<_, unit> =
        fun world ->
            Entity.spawned world |> Observable.add (fun ent ->

                let mutable c = Unchecked.defaultof<'T>
                world.ComponentQuery.TryGet (ent, &c)

                if not <| obj.ReferenceEquals (c, null) then
                    f ent c world
            )

    let inline rule2 (f: Entity -> 'T1 -> 'T2 -> World<_, unit>) : World<_, unit> =
        fun (world: IWorld<'a>) ->
            Entity.spawned world |> Observable.add (fun ent ->

                let mutable c1 = Unchecked.defaultof<'T1>
                world.ComponentQuery.TryGet (ent, &c1)

                if not <| obj.ReferenceEquals (c1, null) then

                    let mutable c2 = Unchecked.defaultof<'T2>
                    world.ComponentQuery.TryGet (ent, &c2)

                    if not <| obj.ReferenceEquals (c2, null) then
                        f ent c1 c2 world
            )