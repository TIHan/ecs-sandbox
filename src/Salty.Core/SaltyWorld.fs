namespace Salty.Core

open System

open ECS.Core

type WorldTime =
    {
        mutable CurrentTime: TimeSpan
        mutable DeltaTime: single
        mutable Interval: TimeSpan
    }

    interface IComponent