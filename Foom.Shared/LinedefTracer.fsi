namespace Foom.Shared.Level

open System.Numerics
open System.Collections.Immutable

open Foom.Shared.Level.Structures

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module LinedefTracer =
    val run : Linedef seq -> Linedef list list