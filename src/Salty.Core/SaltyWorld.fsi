namespace Salty.Core

open System

open ECS.Core

[<Sealed>]
type Var<'T when 'T : equality> =

    member Value : 'T

    interface IObservable<'T>

module Var =

    val create : 'T -> Var<'T>

[<Sealed>]
type Val<'T when 'T : equality> =

    member Value : 'T

    interface IObservable<'T>
    interface IDisposable

module Val =

    val create : 'T -> Val<'T>

    val ofVar : Var<'T> -> Val<'T>

type Salty =
    {
        CurrentTime: Val<TimeSpan>
        DeltaTime: Val<single>
        Interval: Val<TimeSpan>
    }

type SaltyWorld<'T> = World<Salty, 'T>

[<AutoOpen>]
module DSL =

    val DoNothing : SaltyWorld<unit>

    val inline worldReturn : 'a -> SaltyWorld<'a>

    val inline (>>=) : SaltyWorld<'a> -> ('a -> SaltyWorld<'b>) -> SaltyWorld<'b>
    
    val inline (>>.) : SaltyWorld<'a> -> SaltyWorld<'b> -> SaltyWorld<'b>

    val inline skip : SaltyWorld<_> -> SaltyWorld<unit>

    val inline onEvent : SaltyWorld<IObservable<'a>> -> ('a -> SaltyWorld<unit>) -> SaltyWorld<unit>

    val inline onUpdate : IObservable<'a> -> ('a -> SaltyWorld<unit>) -> SaltyWorld<unit>

    val inline (==>) : IObservable<'a> -> Val<'a> -> SaltyWorld<unit>

    val inline update : Var<'a> -> ('a -> 'a) -> SaltyWorld<unit>

    val inline rule : (Entity -> #IComponent -> SaltyWorld<unit>) -> SaltyWorld<unit>

    val inline rule2 : (Entity -> #IComponent -> #IComponent -> SaltyWorld<unit>) -> SaltyWorld<unit>

[<RequireQualifiedAccess>]
module __unsafe =

    val setVarValue : Var<'T> -> 'T -> unit

    val setVarValueWithNotify : Var<'T> -> 'T -> unit

    val setValSource : Val<'T> -> IObservable<'T> -> unit