namespace Foom.Shared.ResourceManager

open System

open Foom.Shared.Level
open Foom.Shared.Wad

type ResourceManager =
    {
        wad: Wad option
    }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module ResourceManager =

    let create () = { wad = None }

    let load fileName rm = async {
        let! wad = Wad.create fileName
        return { rm with wad = Some wad }
    }

    let findLevel levelName rm = async {
        match rm.wad with
        | None -> return failwith "Unable to find level, %s." levelName
        | Some wad -> return! Wad.findLevel levelName wad
    }

