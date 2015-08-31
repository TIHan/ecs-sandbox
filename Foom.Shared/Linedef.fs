namespace Foom.Shared.Level.Structures

open Foom.Shared.Numerics
open System.Numerics

[<NoComparison; ReferenceEquality>]
type Linedef = {
    Start: Vector2
    End: Vector2
    FrontSidedef: Sidedef option
    BackSidedef: Sidedef option }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Linedef =
    let angle (linedef: Linedef) =
        let v = linedef.End - linedef.Start
        Vec2.angle v  
