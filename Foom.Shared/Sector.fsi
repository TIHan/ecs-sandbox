namespace Foom.Shared.Level.Structures

open Foom.Shared.Geometry

type Sector = {
    Linedefs: Linedef [] }

[<CompilationRepresentationAttribute (CompilationRepresentationFlags.ModuleSuffix)>]
module Sector =
    val polygonFlats : Sector -> Polygon list