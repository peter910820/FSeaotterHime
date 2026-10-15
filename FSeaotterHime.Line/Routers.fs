module FSeaotterHime.Line.Routers

open Oxpecker
open FSeaotterHime.Line.Middlewares
open FSeaotterHime.Line.Handlers

let getMainRouter =
    [ POST [ route "/callback" <| (verifySignatureMiddleware >=> eventHandler) ] ]
