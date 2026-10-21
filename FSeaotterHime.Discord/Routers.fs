module FSeaotterHime.Discord.Routers

open Oxpecker
open FSeaotterHime.Discord.HttpHandlers

let getMainRouter =
    [ GET [ route "/youtube/callback" youtubeVerification ]
      POST [ route "/youtube/callback" youtubeNotification ] ]
