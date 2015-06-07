﻿// http://gafferongames.com/game-physics/fix-your-timestep/
module GameLoop

    open System.Diagnostics

    type private GameLoop<'T> = { 
        State: 'T
        LastTime: int64
        UpdateTime: int64
        UpdateAccumulator: int64 }

    let start (state: 'T) updateInterval (pre: unit -> unit) (update: int64 -> int64 -> 'T -> unit) (render: float32 -> 'T -> unit) : unit =
        let targetUpdateInterval = (1000. / updateInterval) * 10000. |> int64
        let skip = (1000. / 5.) * 10000. |> int64

        let stopwatch = Stopwatch.StartNew ()
        let inline time () = stopwatch.Elapsed.Ticks

        let rec loop gl =
            let currentTime = time ()
            let deltaTime =
                match currentTime - gl.LastTime with
                | x when x > skip -> skip
                | x -> x

            let updateAcc = gl.UpdateAccumulator + deltaTime

            let rec processUpdate gl =
                if gl.UpdateAccumulator >= targetUpdateInterval
                then
                    update gl.UpdateTime targetUpdateInterval gl.State

                    processUpdate
                        { gl with 
                            UpdateTime = gl.UpdateTime + targetUpdateInterval
                            UpdateAccumulator = gl.UpdateAccumulator - targetUpdateInterval
                        }
                else
                    gl

            let processRender gl =
                render (single gl.UpdateAccumulator / single targetUpdateInterval) gl.State

                { gl with 
                    LastTime = currentTime
                }

            pre ()
       
            { gl with UpdateAccumulator = updateAcc }
            |> processUpdate
            |> processRender
            |> loop

        loop
            { State = state
              LastTime = 0L
              UpdateTime = 0L
              UpdateAccumulator = targetUpdateInterval
            }