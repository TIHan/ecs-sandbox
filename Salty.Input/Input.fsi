namespace Salty.Core.Input

open ECS.Core

type MouseButtonType =
    | Left = 1
    | Middle = 2
    | Right = 3
    | X1 = 4
    | X2 = 5

[<Struct>]
type MousePosition =
    val X : int
    val Y : int

type InputEvent =
    | KeyPressed of char
    | KeyReleased of char
    | MouseButtonPressed of MouseButtonType
    | MouseButtonReleased of MouseButtonType
    | MouseWheelScrolled of x: int * y: int
    | JoystickButtonPressed of joystickId: int * int
    | JoystickButtonReleased of joystickId: int * int

[<Struct>]
type private KeyboardEvent =
    val IsPressed : int
    val KeyCode : int

[<Struct>]
type private MouseButtonEvent =
    val IsPressed : int
    val Clicks : int
    val Button : MouseButtonType
    val X : int
    val Y : int

[<Struct>]
type private MouseWheelEvent =
    val X : int
    val Y : int

[<Struct>]
type private JoystickButtonEvent =
    val Id : int
    val IsPressed : int
    val Button : int

module internal Input =
    val private dispatchKeyboardEvent : KeyboardEvent -> unit
    val private dispatchMouseButtonEvent : MouseButtonEvent -> unit
    val private dispatchMouseWheelEvent : MouseWheelEvent -> unit
    val private dispatchJoystickButtonEvent : JoystickButtonEvent -> unit
    val getMousePosition : unit -> MousePosition
    val pollEvents : unit -> unit
    val getEvents : unit -> InputEvent list
    val clearEvents : unit -> unit
