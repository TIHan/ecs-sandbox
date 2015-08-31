namespace Foom.Shared.Wad

open System

open Foom.Shared.Level

[<Sealed>]
type Wad

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Wad =

    val create : fileName: string -> Async<Wad>

    val findLevel : levelName: string -> wad: Wad -> Async<Level>
