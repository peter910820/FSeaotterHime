module FSeaotterHime.Line.Routers

open Oxpecker

let getMainRouter = [ GET [ route "/" <| text "Hello, World!" ] ]
