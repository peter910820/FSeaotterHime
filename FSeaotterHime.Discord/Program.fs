module FSeaotterHime.Discord.Program

open Discord
open Discord.WebSocket
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

[<EntryPoint>]
let main args =
    let builder = Host.CreateApplicationBuilder(args)

    let socketConfig =
        DiscordSocketConfig(GatewayIntents = (GatewayIntents.AllUnprivileged ||| GatewayIntents.MessageContent))

    let host = builder.Build()
    use client = new DiscordSocketClient(socketConfig)
    let config = host.Services.GetRequiredService<IConfiguration>()

    let logger =
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FSeaotterHime.Discord")

    BotService.start client config logger
    |> Async.AwaitTask
    |> Async.RunSynchronously

    host.Run()

    BotService.stop client |> Async.AwaitTask |> Async.RunSynchronously
    0
