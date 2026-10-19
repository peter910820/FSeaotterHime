module FSeaotterHime.Discord.HttpHandlers

open System
open Discord
open Discord.WebSocket
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Oxpecker
open type Microsoft.AspNetCore.Http.TypedResults

let sendTestMessage (channelId: uint64) (content: string) : EndpointHandler =
    fun (ctx: HttpContext) ->
        task {
            let logger = ctx.GetLogger()
            let client = ctx.GetService<DiscordSocketClient>()

            if String.IsNullOrWhiteSpace content then
                return! ctx.Write <| BadRequest {| Error = "訊息不可為空" |}
            elif client.LoginState <> LoginState.LoggedIn then
                return!
                    ctx.Write
                    <| Json({| Error = "Discord 尚未連線" |}, statusCode = StatusCodes.Status503ServiceUnavailable)
            else
                let! channel = client.GetChannelAsync(channelId)

                match channel with
                | :? IMessageChannel as messageChannel ->
                    let! message = messageChannel.SendMessageAsync(content)
                    logger.LogInformation("已發送測試訊息 {MessageId} 到 {ChannelId}", message.Id, channelId)
                    return! ctx.Write <| Ok {| MessageId = message.Id |}
                | _ -> return! ctx.Write <| NotFound {| Error = "找不到可發訊的頻道" |}
        }
