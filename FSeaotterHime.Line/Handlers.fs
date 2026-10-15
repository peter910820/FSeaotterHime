module FSeaotterHime.Line.Handlers

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Net.Http.Json
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Oxpecker
open FSeaotterHime.Line.Models

let private httpClient = new HttpClient()

let private fortuneTable =
    [ 90, "你再抽一次🤡"
      85, "仙草吉👑"
      75, "大吉⭐"
      60, "中吉🌟"
      40, "小吉👌"
      20, "末吉🙏"
      5, "凶💀" ]

let private getFortunate () =
    let randNumber = System.Random.Shared.Next(100)

    fortuneTable
    |> List.tryFind (fun (threshold, _) -> randNumber > threshold)
    |> Option.map snd
    |> Option.defaultValue "大凶☠️"

let private replyTextAsync (accessToken: string) (replyToken: string) (text: string) =
    task {
        use request =
            new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/reply")

        request.Headers.Authorization <- AuthenticationHeaderValue("Bearer", accessToken)

        request.Content <-
            JsonContent.Create(
                {| replyToken = replyToken
                   messages = [ {| ``type`` = "text"; text = text |} ] |}
            )

        let! response = httpClient.SendAsync(request)
        response.EnsureSuccessStatusCode() |> ignore
    }

let eventHandler: EndpointHandler =
    fun (ctx: HttpContext) ->
        task {
            let logger = ctx.GetLogger()
            let accessToken = ctx.GetService<IConfiguration>()["LineBot:ChannelAccessToken"]
            let! payload = ctx.BindJson<LineWebhookPayload>()

            // logger.LogInformation(
            //     "Destination: {Destination}, 事件數量: {Count}",
            //     payload.Destination,
            //     payload.Events.Length
            // )

            for event in payload.Events do
                match event.Type, event.ReplyToken, event.Message with
                | "message", Some replyToken, Some msg when msg.Type = "text" ->
                    let text = msg.Text |> Option.defaultValue ""
                    logger.LogInformation("收到文字訊息: {Text}", text)

                    let replyMessageOpt =
                        match text with
                        | t when t.Contains "運" -> Some <| $"💫您今天的運勢: {getFortunate ()}"
                        | _ -> None

                    // send reply message
                    match replyMessageOpt with
                    | Some replyMessage -> do! replyTextAsync accessToken replyToken replyMessage
                    | None -> ()
                | _ -> logger.LogInformation("收到其他事件: {Type}", event.Type)

            return! ctx.WriteText "Success!"
        }
