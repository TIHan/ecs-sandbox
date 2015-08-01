namespace Salty.Input

type MouseButtonType =
    | Left = 1
    | Middle = 2
    | Right = 3
    | X1 = 4
    | X2 = 5

type InputEvent =
    | KeyPressed of char
    | KeyReleased of char
    | MouseButtonPressed of MouseButtonType
    | MouseButtonReleased of MouseButtonType
    | MouseWheelScrolled of x: int * y: int
    | JoystickButtonPressed of int
    | JoystickButtonReleased of int

[<Struct>]
type MousePosition =
    val X : int
    val Y : int

[<Struct>]
type KeyboardEvent =
    val IsPressed : int
    val KeyCode : int

[<Struct>]
type MouseButtonEvent =
    val IsPressed : int
    val Clicks : int
    val Button : MouseButtonType
    val X : int
    val Y : int

[<Struct>]
type MouseWheelEvent =
    val X : int
    val Y : int

[<Struct>]
type JoystickButtonEvent =
    val IsPressed : int
    val Button : int

module Input =
    val dispatchKeyboardEvent : KeyboardEvent -> unit
    val dispatchMouseButtonEvent : MouseButtonEvent -> unit
    val dispatchMouseWheelEvent : MouseWheelEvent -> unit
    val dispatchJoystickButtonEvent : JoystickButtonEvent -> unit
    val pollEvents : unit -> unit
    val getEvents : unit -> InputEvent list
    val clearEvents : unit -> unit
    val getMousePosition : unit -> MousePosition
    val isKeyPressed : char -> bool
    val isMouseButtonPressed : MouseButtonType -> bool
    val isJoystickButtonPressed : int -> bool
