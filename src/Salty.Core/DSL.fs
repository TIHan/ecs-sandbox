namespace Salty.Core

open System

open ECS.Core

[<AutoOpen>]
module Observable =

    let inline (<~) f (source: IObservable<'a>) = Observable.map f source

    let inline (<*>) (fSource: IObservable<('a -> 'b)>) (source: IObservable<'a>) : IObservable<'b> =      
        {
            new IObservable<'b> with

                member __.Subscribe observer =
                    let refA = ref None
                    let refF = ref None
                    let s1 = 
                        source 
                        |> Observable.subscribe (fun a -> refA := Some a; match !refF with | Some f -> observer.OnNext (f a) | _ -> ())
                    let s2 = 
                        fSource 
                        |> Observable.subscribe (fun f -> refF := Some f; match !refA with | Some a -> observer.OnNext (f a) | _ -> ())
                    {
                        new IDisposable with

                            member __.Dispose () =
                                s1.Dispose ()
                                s2.Dispose ()
                    }
        }

    let inline distinct<'a when 'a : equality> (source: IObservable<'a>) =
        {
            new IObservable<'a> with

                member __.Subscribe observer =
                    let refA : 'a option ref = ref None
                    source
                    |> Observable.subscribe (fun a ->
                        match !refA with
                        | None ->
                            refA := Some a
                            observer.OnNext a
                        | Some existing when not <| a.Equals existing ->
                            refA := Some a
                            observer.OnNext a
                        | _ -> ()
                    )
        }

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
        fun world -> v.Listen source

    let inline (<--) (var: Var<'T>) (value: 'T) : World<_, unit> =
        fun world -> var.Value <- value

    let inline source (v: Val<'a>) (source: IObservable<'a>) : World<_, unit> =
        fun world -> v.Listen source

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