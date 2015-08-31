[<AutoOpen>]
module FSharp.LitePickler.Core

type StringKind =
    | Default
    | EightBit
    | ASCII
    | BigEndianUnicode
    | Unicode
    | UTF32
    | UTF7
    | UTF8