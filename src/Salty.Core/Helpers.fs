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

[<AutoOpen>]
module Helpers =

    let DoNothing : SaltyWorld<unit> = fun _ -> ()

    let inline (>>=) (w: SaltyWorld<'a>) (f: 'a -> SaltyWorld<'b>) : SaltyWorld<'b> =
        fun world -> (f (w world)) world

    let inline upon (w: SaltyWorld<IObservable<'a>>) (f: 'a -> SaltyWorld<unit>) : SaltyWorld<unit> =
        fun world ->
            (w world) 
            |> Observable.add (fun a ->
                (f a) world
            )

    let inline push (f: 'a -> SaltyWorld<unit>) (source: IObservable<'a>) : SaltyWorld<unit> =
        fun world -> source |> Observable.add (fun x -> (f x) world)

    let inline pushTo (var: Var<'a>) (source: IObservable<'a>) : SaltyWorld<unit> =
        fun world -> var.Listen source

    let inline uponSpawn (f: Entity -> 'T -> SaltyWorld<unit> list) : SaltyWorld<unit> =
        fun world ->
            Entity.spawned world |> Observable.add (fun ent ->

                let mutable c = Unchecked.defaultof<'T>
                world.ComponentQuery.TryGet (ent, &c)

                if not <| obj.ReferenceEquals (c, null) then
                    f ent c
                    |> List.iter (fun x -> x world)
            )

    let inline uponSpawn2 (f: Entity -> 'T1 -> 'T2 -> SaltyWorld<unit> list) : SaltyWorld<unit> =
        fun world ->
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