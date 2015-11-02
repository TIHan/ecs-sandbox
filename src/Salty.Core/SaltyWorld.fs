namespace Salty.Core

open System

open ECS.Core

type Salty =
    {
        CurrentTime: Val<TimeSpan>
        DeltaTime: Val<single>
        Interval: Val<TimeSpan>
    }

type SaltyWorld<'T> = World<Salty> -> 'T