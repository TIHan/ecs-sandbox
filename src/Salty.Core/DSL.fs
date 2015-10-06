namespace Salty.Core

open System

open ECS.Core

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

    let inline sink (f: 'a -> World<_, unit>) (source: IObservable<'a>) : World<_, unit> =
        fun world -> source |> Observable.add (fun x -> (f x) world)

    let inline (==>) (source: IObservable<'a>) (v: Val<'a>) : World<_, unit> =
        fun world -> v.UpdatesOn source

    let inline (<~) (source: IObservable<'a>) f = Observable.map f source
            
    let inline update (v: Var<'a>) (f: 'a -> 'a) : World<_, unit> =
        fun world -> v.Value <- f v.Value 

    let inline rule (f: Entity -> 'T -> World<_, unit> list) : World<_, unit> =
        fun world ->
            Entity.spawned world |> Observable.add (fun ent ->

                let mutable c = Unchecked.defaultof<'T>
                world.ComponentQuery.TryGet (ent, &c)

                if not <| obj.ReferenceEquals (c, null) then
                    f ent c
                    |> List.iter (fun x -> x world)
            )

    let inline rule2 (f: Entity -> 'T1 -> 'T2 -> World<_, unit> list) : World<_, unit> =
        fun (world: IWorld<'a>) ->
            Entity.spawned world |> Observable.add (fun ent ->

                let mutable c1 = Unchecked.defaultof<'T1>
                let mutable c2 = Unchecked.defaultof<'T2>
                world.ComponentQuery.TryGet (ent, &c1)
                world.ComponentQuery.TryGet (ent, &c2)

                if 
                    not <| obj.ReferenceEquals (c1, null) &&
                    not <| obj.ReferenceEquals (c2, null)
                then
                    f ent c1 c2
                    |> List.iter (fun x -> x world)
            )