module FSeaotterHime.Line.Routers

open Oxpecker
open FSeaotterHime.Line.Middlewares

let getMainRouter = [ 
    GET [ route "/" <| text "Hello, World!" ]
    POST [ route "/callback" <| (verifySignatureMiddleware >=> text "Success!" ) ] ]
