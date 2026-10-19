module FSeaotterHime.Discord.Routers

open Oxpecker
open FSeaotterHime.Discord.HttpHandlers

let getMainRouter = [ GET [ routef "/{%u}/{%s}" sendTestMessage ] ]
