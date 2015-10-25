namespace Salty.Core.Renderer

open System
open System.IO
open System.Numerics
open System.Collections.Generic
open System.Runtime.InteropServices

open Microsoft.FSharp.NativeInterop

open Ferop

#nowarn "9"

[<Struct>]
type RendererContext =
    val Window : nativeint
    val GLContext : nativeint

type VBO = VBO of id: int * size: int

type VAO = VAO of id: int

type Attribute = Attribute of id: int

type Window = Window of nativeint

module TextureCache =
    let Cache = Dictionary<string, int> ()

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
#if defined(__GNUC__)
#   include "SDL.h"
#   include "SDL_opengl.h"
#else
#   include "SDL.h"
#   include <GL/glew.h>
#   include <GL/wglew.h>
#endif
""")>]
[<Source ("""
char VertexShaderErrorMessage[65536];
char FragmentShaderErrorMessage[65536];
char ProgramErrorMessage[65536];
""")>]
type R private () = 

    [<Export>]
    static member private Failwith (size: int, ptr: nativeptr<sbyte>) : unit =
        let str = String (ptr)
        failwith str

    [<Import; MI (MIO.NoInlining)>]
    static member private _CreateBuffer_vector2 (size: int, data: Vector2 []) : int =
        C """
        GLuint buffer;

        glGenBuffers (1, &buffer);
        glBindBuffer (GL_ARRAY_BUFFER, buffer);
        glBufferData (GL_ARRAY_BUFFER, size, data, GL_DYNAMIC_DRAW);
        glBindBuffer (GL_ARRAY_BUFFER, 0);

        return buffer;
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _LoadShaders (vertexSource: byte[]) (fragmentSource: byte[]) : int =
        C """
        // Create the shaders
        GLuint VertexShaderID = glCreateShader(GL_VERTEX_SHADER);
        GLuint FragmentShaderID = glCreateShader(GL_FRAGMENT_SHADER);

        GLint Result = GL_FALSE;
        int InfoLogLength;



        // Compile Vertex Shader
        glShaderSource(VertexShaderID, 1, &vertexSource, NULL);
        glCompileShader(VertexShaderID);

        // Check Vertex Shader
        glGetShaderiv(VertexShaderID, GL_COMPILE_STATUS, &Result);
        glGetShaderiv(VertexShaderID, GL_INFO_LOG_LENGTH, &InfoLogLength);
        if ( InfoLogLength > 0 ){
            glGetShaderInfoLog(VertexShaderID, InfoLogLength, &InfoLogLength, &VertexShaderErrorMessage[0]);
            if (InfoLogLength > 0)
            {
                R_Failwith(InfoLogLength, &VertexShaderErrorMessage[0]);
            }
            for (int i = 0; i < 65536; ++i) { VertexShaderErrorMessage[i] = '\0'; }
        }



        // Compile Fragment Shader
        glShaderSource(FragmentShaderID, 1, &fragmentSource, NULL);
        glCompileShader(FragmentShaderID);

        // Check Fragment Shader
        glGetShaderiv(FragmentShaderID, GL_COMPILE_STATUS, &Result);
        glGetShaderiv(FragmentShaderID, GL_INFO_LOG_LENGTH, &InfoLogLength);
        if ( InfoLogLength > 0 ){
            glGetShaderInfoLog(FragmentShaderID, InfoLogLength, &InfoLogLength, &FragmentShaderErrorMessage[0]);
            if (InfoLogLength > 0)
            {
                R_Failwith(InfoLogLength, &FragmentShaderErrorMessage[0]);
            }
            for (int i = 0; i < 65536; ++i) { FragmentShaderErrorMessage[i] = '\0'; }
        }



        // Link the program
        printf("Linking program\n");
        GLuint ProgramID = glCreateProgram();
        glAttachShader(ProgramID, VertexShaderID);
        glAttachShader(ProgramID, FragmentShaderID);


        glLinkProgram(ProgramID);

        // Check the program
        glGetProgramiv(ProgramID, GL_LINK_STATUS, &Result);
        glGetProgramiv(ProgramID, GL_INFO_LOG_LENGTH, &InfoLogLength);
        if ( InfoLogLength > 0 ){
            glGetProgramInfoLog(ProgramID, InfoLogLength, &InfoLogLength, &ProgramErrorMessage[0]);
            if (InfoLogLength > 0)
            {
               // R_Failwith(InfoLogLength, &ProgramErrorMessage[0]);
            }
            for (int i = 0; i < 65536; ++i) { ProgramErrorMessage[i] = '\0'; }
        }

        return ProgramID;
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _CreateTexture (width: int) (height: int) (data: nativeint) : int =
        C """
        // Create one OpenGL texture
        GLuint textureID;
        glGenTextures(1, &textureID);
         
        // "Bind" the newly created texture : all future texture functions will modify this texture
        glBindTexture(GL_TEXTURE_2D, textureID);
         
        // Give the image to OpenGL
        glTexImage2D(GL_TEXTURE_2D, 0,GL_RGB, width, height, 0, GL_RGB, GL_UNSIGNED_BYTE, data);
         
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
        return textureID;
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _CreateWindow () : nativeint =
        C """
        return
        SDL_CreateWindow(
            "ECS",
            SDL_WINDOWPOS_UNDEFINED,
            SDL_WINDOWPOS_UNDEFINED,
            1280, 720,
            SDL_WINDOW_OPENGL);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _Init (window: nativeint) : RendererContext =
        C """
        R_RendererContext r;

        r.Window = window;

        SDL_GL_SetAttribute (SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL_GL_SetAttribute (SDL_GL_CONTEXT_MINOR_VERSION, 3);
        SDL_GL_SetAttribute (SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);

        r.GLContext = SDL_GL_CreateContext ((SDL_Window*)r.Window);
        SDL_GL_SetSwapInterval (1);


        #if defined(__GNUC__)
        #else
        glewExperimental = GL_TRUE;
        glewInit ();
        #endif

        return r;
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _CreateVao () : int =
        C """
        GLuint vao;
        glGenVertexArrays (1, &vao);

        glBindVertexArray (vao);

        return vao;
        """

    static member CreateVao () : VAO =
        let id = R._CreateVao ()
        VAO id

    [<Import; MI (MIO.NoInlining)>]
    static member UseProgram (program: int) : unit =
        C """
        glUseProgram (program);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member InitSDL () : unit =
        C """
        SDL_Init (SDL_INIT_VIDEO | SDL_INIT_JOYSTICK);
        """

    static member CreateWindow () : Window =
        let p = R._CreateWindow ()
        Window p

    static member Init (Window (p): Window) : RendererContext =
        R._Init (p)

    [<Import; MI (MIO.NoInlining)>]
    static member Exit (r: RendererContext) : int =
        C """
        SDL_GL_DeleteContext (r.GLContext);
        SDL_DestroyWindow ((SDL_Window*)r.Window);
        SDL_Quit ();
        return 0;
        """
    
    [<Import; MI (MIO.NoInlining)>]
    static member Clear () : unit = 
        C """ 
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);    
        """

    [<Import; MI (MIO.NoInlining)>]
    static member EnableDepth () : unit =
        C """
	    // Enable depth test
	    glEnable(GL_DEPTH_TEST);
	    // Accept fragment if it closer to the camera than the former one
	    glDepthFunc(GL_LESS); 

	    // Cull triangles which normal is not towards the camera
	    glEnable(GL_CULL_FACE);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member DisableDepth () : unit =
        C """
        glDisable(GL_CULL_FACE);
        glDisable(GL_DEPTH_TEST);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member Draw (r: RendererContext) : unit = C """ SDL_GL_SwapWindow ((SDL_Window*)r.Window); """

    static member CreateVBO (data: Vector2 []) : VBO =
        let size = data.Length * sizeof<Vector2>
        let id = R._CreateBuffer_vector2 (size, data)
        VBO (id, data.Length)

    [<Import; MI (MIO.NoInlining)>]
    static member private _GetUniformLocation (shaderProgramId: int) (name: nativeptr<sbyte>) : unit =
        C """
        return glGetUniformLocation (shaderProgramId, name);
        """

    static member GetUniformLocation (shaderProgramId: int) (name: string) : unit =
        let handle = GCHandle.Alloc (name, GCHandleType.Pinned)
        let addr = handle.AddrOfPinnedObject () |> NativePtr.ofNativeInt<sbyte>
        let result = R._GetUniformLocation (shaderProgramId) addr
        handle.Free ()
        result

    [<Import; MI (MIO.NoInlining)>]
    static member UniformInt (uniformId: int) (value: int) : unit =
        C """
        return glUniform1i(uniformId, value);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member SetColor (shaderProgram: int) (r: single) (g: single) (b: single) : unit = 
        C """
        GLint uni_color = glGetUniformLocation (shaderProgram, "uni_color");
        glUniform4f (uni_color, r, g, b, 1.0f);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member SetProjection (shaderProgram: int) (projection: Matrix4x4) : unit =
        C """
        GLuint uni = glGetUniformLocation (shaderProgram, "uni_projection");
        glUniformMatrix4fv (uni, 1, GL_FALSE, &projection);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member SetView (shaderProgram: int) (view: Matrix4x4) : unit =
        C """
        GLuint uni = glGetUniformLocation (shaderProgram, "uni_view");
        glUniformMatrix4fv (uni, 1, GL_FALSE, &view);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member SetModel (shaderProgram: int) (model: Matrix4x4) : unit =
        C """
        GLuint uni = glGetUniformLocation (shaderProgram, "uni_model");
        glUniformMatrix4fv (uni, 1, GL_FALSE, &model);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member SetCameraPosition (shaderProgram: int) (cameraPosition: Vector3) : unit =
        C """
        GLuint uni = glGetUniformLocation (shaderProgram, "uni_cameraPosition");
        glUniform3f (uni, cameraPosition.X, cameraPosition.Y, cameraPosition.Z);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member SetTexture (shaderProgram: int) (textureId: int) : unit =
        C """
        GLuint uni = glGetUniformLocation (shaderProgram, "uni_texture");
        glUniform1i(textureId, 0);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member BindTexture (textureId: int) : unit =
        C """
        glActiveTexture(GL_TEXTURE0);
        glBindTexture(GL_TEXTURE_2D, textureId);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _DrawLines (programId: int, vboId: int, size : int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vboId);

        GLint posAttrib = glGetAttribLocation (programId, "position");
        glVertexAttribPointer (posAttrib, 2, GL_FLOAT, GL_FALSE, 0, 0);
        glEnableVertexAttribArray (posAttrib);

        glDrawArrays (GL_LINES, 0, size);

        glDisableVertexAttribArray (posAttrib);

        glBindBuffer (GL_ARRAY_BUFFER, 0);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _DrawLineLoop (programId: int, vboId: int, size : int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vboId);

        GLint posAttrib = glGetAttribLocation (programId, "position");
        glVertexAttribPointer (posAttrib, 2, GL_FLOAT, GL_FALSE, 0, 0);
        glEnableVertexAttribArray (posAttrib);

        glDrawArrays (GL_LINE_LOOP, 0, size);

        glDisableVertexAttribArray (posAttrib);

        glBindBuffer (GL_ARRAY_BUFFER, 0);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _DrawTriangles (size : int) : unit =
        C """
        //glBindBuffer (GL_ARRAY_BUFFER, vboId);

        //GLint posAttrib = glGetAttribLocation (programId, "position");
        //glVertexAttribPointer (posAttrib, 2, GL_FLOAT, GL_FALSE, 0, 0);
        //glEnableVertexAttribArray (posAttrib);

        glDrawArrays (GL_TRIANGLES, 0, size);

        //glDisableVertexAttribArray (posAttrib);

        //glBindBuffer (GL_ARRAY_BUFFER, 0);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _BindArrayBuffer (vboId: int) : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, vboId);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _UnbindArrayBuffer () : unit =
        C """
        glBindBuffer (GL_ARRAY_BUFFER, 0);
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _BindAttribute (programId: int, name: nativeint) : int =
        C """
        GLint attrib = glGetAttribLocation (programId, name);
        glVertexAttribPointer (attrib, 2, GL_FLOAT, GL_FALSE, 0, 0);
        glEnableVertexAttribArray (attrib);
        return attrib;
        """

    [<Import; MI (MIO.NoInlining)>]
    static member private _UnbindAttribute (attribId: int) : unit =
        C """
        glDisableVertexAttribArray (attribId);
        """

    static member DrawLines programId (VBO (id, size): VBO) : unit =
        R._DrawLines (programId, id, size)

    static member DrawLineLoop programId (VBO (id, size): VBO) : unit =
        R._DrawLineLoop (programId, id, size)

    static member DrawTriangles (VBO (id, size): VBO) : unit =
        R._DrawTriangles (size)

    static member BindArrayBuffer (VBO (id, size): VBO) : unit =
        R._BindArrayBuffer (id)

    static member UnbindArrayBuffer () : unit =
        R._UnbindArrayBuffer ()

    static member BindAttribute programId (name: string) : Attribute =
        let encoding = System.Text.Encoding.UTF8

        let bytes = encoding.GetBytes (name)
        let alloc = GCHandle.Alloc (bytes, GCHandleType.Pinned)
        let addr = alloc.AddrOfPinnedObject ()

        let result = R._BindAttribute (programId, addr) |> Attribute

        alloc.Free ()
        result

    static member UnbindAttribute (Attribute (id)) =
        R._UnbindAttribute (id)

    static member CreateTexture (fileName: string) : int =
        match TextureCache.Cache.ContainsKey fileName with
        | true -> 
            TextureCache.Cache.[fileName]
        | _ ->

        use bmp = new Gdk.Pixbuf (fileName)
        let id = R._CreateTexture bmp.Width bmp.Height bmp.Pixels

        TextureCache.Cache.Add (fileName, id)
        id

    static member LoadShaders (vertexFile, fragmentFile) =
        printfn "Loading %A and %A" vertexFile fragmentFile
        let mutable vertexFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (vertexFile))
        let mutable fragmentFile = ([|0uy|]) |> Array.append (File.ReadAllBytes (fragmentFile))

        R._LoadShaders vertexFile fragmentFile


module Shader =

    [<ReferenceEquality>]
    type Uniform<'T> =
        private {
            mutable value: 'T
            mutable bind: unit -> unit
            mutable id: int
        }

        member this.Bind () = this.bind ()

        member this.Value
            with get () = this.value
            and set value = this.value <- value

    type Uniform private () =

        static member Create (value: int32) =
            let uniform =
                {
                    value = value
                    bind = id
                    id = -1
                }

            uniform.bind <- fun () -> 
                if not <| uniform.id.Equals -1 then
                    R.UniformInt uniform.id uniform.value
            
            uniform

    type Test =
        {
            uValue: Uniform<int32>
        }

    type ShaderSource =
        | Full of string
        | Partial of string

    [<ReferenceEquality>]
    type Program<'T> =
        private {
            mutable shader: 'T
            mutable programId: int
            mutable vertexShaderSource: ShaderSource
            mutable fragmentShaderSource: ShaderSource
            uniformLocationFuncs: ResizeArray<unit -> unit>
        }

        static member Create (shader: 'T, vertexShaderSource: ShaderSource, fragmentShaderSource: ShaderSource) =
            let t = typeof<'T>

            let program =
                {
                    shader = shader
                    programId = -1
                    vertexShaderSource = vertexShaderSource
                    fragmentShaderSource = fragmentShaderSource
                    uniformLocationFuncs = ResizeArray ()
                }

            let props = 
                t.GetProperties ()
                |> Seq.filter (fun prop -> prop.DeclaringType.Name = "Uniform`1")
            
            let idFields =
                props
                |> Seq.map (fun prop -> prop.DeclaringType.GetField ("id"))

            (props, idFields)
            ||> Seq.iter2 (fun prop idField ->
                program.uniformLocationFuncs.Add (fun () ->
                    if not <| program.programId.Equals -1 then
                        idField.SetValue (prop, R.GetUniformLocation program.programId prop.Name)
                )
            )

            program

        member this.VertexShaderSource
            with get () = this.vertexShaderSource
            and set value = this.vertexShaderSource <- value

        member this.FragmentShaderSource
            with get () = this.fragmentShaderSource
            and set value = this.fragmentShaderSource <- value

        member this.Compile () =
            ()

        member this.Use () =
            if this.programId <> -1 then
                R.UseProgram (this.programId)