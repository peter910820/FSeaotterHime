module FSeaotterHime.Discord.BotService

open System
open System.Threading.Tasks
open Discord
open Discord.WebSocket
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging

let writeLog (logger: ILogger) (message: LogMessage) =
    match message.Severity with
    | LogSeverity.Critical
    | LogSeverity.Error -> logger.LogError(message.Exception, "{Source}: {Message}", message.Source, message.Message)
    | LogSeverity.Warning -> logger.LogWarning("{Source}: {Message}", message.Source, message.Message)
    | LogSeverity.Info -> logger.LogInformation("{Source}: {Message}", message.Source, message.Message)
    | _ -> logger.LogDebug("{Source}: {Message}", message.Source, message.Message)


let start (client: DiscordSocketClient) (config: IConfiguration) (logger: ILogger) =

    client.add_Log (Func<LogMessage, Task>(fun message -> task { writeLog logger message } :> Task))
    Handlers.register client logger

    match
        config["Discord:Token"]
        |> Option.ofObj
        |> Option.filter (not << String.IsNullOrWhiteSpace)
    with
    | None ->
        logger.LogError("Discord:Token 未設定")
        Task.CompletedTask
    | Some token ->
        task {
            do! client.LoginAsync(TokenType.Bot, token)
            do! client.StartAsync()
        }
        :> Task

let stop (client: DiscordSocketClient) = client.StopAsync()
