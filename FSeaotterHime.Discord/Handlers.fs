module FSeaotterHime.Discord.Handlers

open System
open System.Threading.Tasks
open Discord.WebSocket
open Microsoft.Extensions.Logging

let private replyText (message: SocketMessage) =
    if message.Author.IsBot || String.IsNullOrWhiteSpace message.Content then
        None
    else
        Some message.Content

let private echo (message: SocketMessage) =
    match replyText message with
    | None -> Task.CompletedTask
    | Some text -> message.Channel.SendMessageAsync(text) :> Task

let register (client: DiscordSocketClient) (logger: ILogger) =
    client.add_Ready (Func<Task>(fun () -> task { logger.LogInformation("已登入 {User}", client.CurrentUser) } :> Task))
    client.add_MessageReceived (Func<SocketMessage, Task>(echo))
