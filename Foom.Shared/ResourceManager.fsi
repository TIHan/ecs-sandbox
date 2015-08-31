namespace Foom.Shared.ResourceManager

open System

open Foom.Shared.Level

[<Sealed>]
type ResourceManager

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module ResourceManager =

    val create : unit -> ResourceManager

    val load : fileName: string -> rm: ResourceManager -> Async<ResourceManager>

    val findLevel : levelName: string -> rm: ResourceManager -> Async<Level>

