module FSeaotterHime.Discord.HttpHandlers

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Oxpecker
open type Microsoft.AspNetCore.Http.TypedResults

let youtubeVerification : EndpointHandler =
    fun (ctx: HttpContext) ->
        task {
            let logger = ctx.GetLogger()
            let query = ctx.Request.Query
            let mode = string query["hub.mode"]
            let topic = string query["hub.topic"]
            let challenge = string query["hub.challenge"]
            let verifyToken = string query["hub.verify_token"]
            let expectedToken = ctx.GetService<IConfiguration>()["PubSubHubbub:VerifyToken"]

            let deny (reason: string) =
                logger.LogWarning("拒絕 YouTube 訂閱驗證：{Reason}", reason)
                ctx.Write <| StatusCode StatusCodes.Status404NotFound

            if String.IsNullOrEmpty expectedToken || verifyToken <> expectedToken then
                return! deny "verify_token 不符"
            elif String.IsNullOrEmpty challenge then
                return! deny "缺少 hub.challenge"
            elif mode <> "subscribe" && mode <> "unsubscribe" then
                return! deny "hub.mode 不正確"
            else
                logger.LogInformation("確認 YouTube {Mode} {Topic}", mode, topic)
                return! ctx.Write <| Text(challenge, "text/plain")
        }

let youtubeNotification : EndpointHandler =
    fun (ctx: HttpContext) ->
        ctx.GetLogger().LogInformation("收到 YouTube 推播")
        Task.CompletedTask
