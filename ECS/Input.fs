namespace Input

open Ferop

open System.Collections.Generic
open System.Runtime.InteropServices

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
    | JoystickButtonPressed of int
    | JoystickButtonReleased of int

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

[<Ferop>]
[<ClangOsx (
    "-DGL_GLEXT_PROTOTYPES -I/Library/Frameworks/SDL2.framework/Headers",
    "-F/Library/Frameworks -framework Cocoa -framework OpenGL -framework IOKit -framework SDL2"
)>]
[<GccLinux ("-I../../include/SDL2", "-lSDL2")>]
#if __64BIT__
[<MsvcWin ("""/O2 /I ..\include\SDL2 /I ..\include ..\lib\win\x64\SDL2.lib ..\lib\win\x64\SDL2main.lib ..\lib\win\x64\glew32.lib opengl32.lib """)>]
#else
[<MsvcWin ("""/O2 /I  ..\include\SDL2 /I  ..\include  ..\lib\win\x86\SDL2.lib  ..\lib\win\x86\SDL2main.lib  ..\lib\win\x86\glew32.lib opengl32.lib """)>]
#endif
[<Header ("""
#include <stdio.h>
#include "SDL.h"
""")>]
module Input =

    let inputEvents = ResizeArray<InputEvent> ()

    let keyPressedSet = HashSet<char> ()

    let mouseButtonPressedSet = HashSet<MouseButtonType> ()

    let joystickButtonPressedSet = HashSet<int> ()

    [<Export>]
    let dispatchKeyboardEvent (kbEvt: KeyboardEvent) : unit =
        inputEvents.Add (
            let key = char kbEvt.KeyCode
            if kbEvt.IsPressed = 0 then 
                keyPressedSet.Remove key |> ignore
                InputEvent.KeyReleased key
            else 
                keyPressedSet.Add key |> ignore
                InputEvent.KeyPressed key
        )

    [<Export>]
    let dispatchMouseButtonEvent (mbEvt: MouseButtonEvent) : unit =
        inputEvents.Add (
            let btn = mbEvt.Button
            if mbEvt.IsPressed = 0 then
                mouseButtonPressedSet.Remove btn |> ignore
                InputEvent.MouseButtonReleased btn
            else
                mouseButtonPressedSet.Add btn |> ignore
                InputEvent.MouseButtonPressed btn
        )

    [<Export>]
    let dispatchMouseWheelEvent (evt: MouseWheelEvent) : unit =
        inputEvents.Add (InputEvent.MouseWheelScrolled (evt.X, evt.Y))

    [<Export>]
    let dispatchJoystickButtonEvent (jEvt: JoystickButtonEvent) : unit =
        let btn = jEvt.Button
        let isPressed = jEvt.IsPressed = 1
        if not (joystickButtonPressedSet.Contains btn) && isPressed then 
            inputEvents.Add (
                joystickButtonPressedSet.Add btn |> ignore
                InputEvent.JoystickButtonPressed btn
            )
        elif joystickButtonPressedSet.Contains btn && not isPressed then
            inputEvents.Add (
                joystickButtonPressedSet.Remove btn |> ignore
                InputEvent.JoystickButtonReleased btn
            )


    [<Import; MI (MIO.NoInlining)>]
    let pollEvents () : unit =
        C """
        SDL_Event e;
        while (SDL_PollEvent (&e))
        {
            if (e.type == SDL_KEYDOWN)
            {
                SDL_KeyboardEvent* event = (SDL_KeyboardEvent*)&e;
                if (event->repeat != 0) continue;

                Input_KeyboardEvent evt;
                evt.IsPressed = 1;
                evt.KeyCode = event->keysym.sym;
                Input_dispatchKeyboardEvent (evt);
            }
            else if (e.type == SDL_KEYUP)
            {
                SDL_KeyboardEvent* event = (SDL_KeyboardEvent*)&e;
                if (event->repeat != 0) continue;

                Input_KeyboardEvent evt;
                evt.IsPressed = 0;
                evt.KeyCode = event->keysym.sym;

                Input_dispatchKeyboardEvent (evt);
            }
            else if (e.type == SDL_MOUSEBUTTONDOWN)
            {
                SDL_MouseButtonEvent* event = (SDL_MouseButtonEvent*)&e;
        
                Input_MouseButtonEvent evt;
                evt.IsPressed = 1;
                evt.Clicks = event->clicks;
                evt.Button = event->button;
                evt.X = event->x;
                evt.Y = event->y;

                Input_dispatchMouseButtonEvent (evt);
            }
            else if (e.type == SDL_MOUSEBUTTONUP)
            {
                SDL_MouseButtonEvent* event = (SDL_MouseButtonEvent*)&e;
        
                Input_MouseButtonEvent evt;
                evt.IsPressed = 0;
                evt.Clicks = event->clicks;
                evt.Button = event->button;
                evt.X = event->x;
                evt.Y = event->y;

                Input_dispatchMouseButtonEvent (evt);
            }
            else if (e.type == SDL_MOUSEWHEEL)
            {
                SDL_MouseWheelEvent* event = (SDL_MouseWheelEvent*)&e;
        
                Input_MouseWheelEvent evt;
                evt.X = event->x;
                evt.Y = event->y;

                Input_dispatchMouseWheelEvent (evt);
            }
        }
        
  int num_joysticks = SDL_NumJoysticks();
  int i;
  for(i = 0; i < num_joysticks; ++i)
  {
    SDL_Joystick* js = SDL_JoystickOpen(i);
    if (js)
    {
      SDL_JoystickGUID guid = SDL_JoystickGetGUID(js);
      char guid_str[1024];
      SDL_JoystickGetGUIDString(guid, guid_str, sizeof(guid_str));
      const char* name = SDL_JoystickName(js);

      int num_axes = SDL_JoystickNumAxes(js);
      int num_buttons = SDL_JoystickNumButtons(js);
      int num_hats = SDL_JoystickNumHats(js);
      int num_balls = SDL_JoystickNumBalls(js);

//      printf("%s \"%s\" axes:%d buttons:%d hats:%d balls:%d\n", 
//             guid_str, name,
//             num_axes, num_buttons, num_hats, num_balls);

      for (int j = 0; j < num_buttons; ++j) {
        uint8_t isPressed = SDL_JoystickGetButton (js, j);

        Input_JoystickButtonEvent evt;
        evt.IsPressed = (int32_t)isPressed;
        evt.Button = j;

        Input_dispatchJoystickButtonEvent (evt);
      }

      SDL_JoystickClose(js);
    }
  }
        """

    let getEvents () = 
        let events = inputEvents.ToArray ()
        events
        |> Seq.distinct
        |> List.ofSeq

    let clearEvents () =
        inputEvents.Clear ()

    [<Import; MI (MIO.NoInlining)>]
    let getMousePosition () : MousePosition =
        C """
        int32_t x;
        int32_t y;
        Input_MousePosition state;
        SDL_GetMouseState (&x, &y);
        state.X = x;
        state.Y = y;
        return state;
        """

    let isKeyPressed key = keyPressedSet.Contains key

    let isMouseButtonPressed btn = mouseButtonPressedSet.Contains btn

    let isJoystickButtonPressed btn = joystickButtonPressedSet.Contains btn
        