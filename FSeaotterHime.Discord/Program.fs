module FSeaotterHime.Discord.Program

open System
open System.Threading.Tasks
open Discord
open Discord.WebSocket
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Oxpecker
open type Microsoft.AspNetCore.Http.TypedResults
open FSeaotterHime.Discord.Routers

let errorHandler (ctx: HttpContext) (next: RequestDelegate) =
    task {
        try
            return! next.Invoke(ctx)
        with
        | :? ModelBindException
        | :? RouteParseException as ex ->
            let logger = ctx.GetLogger()
            logger.LogWarning(ex, "Unhandled 400 error")
            return! ctx.Write <| BadRequest {| Error = ex.Message |}
        | ex ->
            let logger = ctx.GetLogger()
            logger.LogError(ex, "Unhandled 500 error")
            ctx.SetStatusCode StatusCodes.Status500InternalServerError
            return! ctx.WriteText <| string ex
    }
    :> Task

let notFoundHandler (ctx: HttpContext) =
    let logger = ctx.GetLogger()
    logger.LogWarning("Unhandled 404 error")
    ctx.Write <| NotFound {| Error = "Resource was not found" |}

let configureServices (services: IServiceCollection) =
    let socketConfig =
        DiscordSocketConfig(GatewayIntents = (GatewayIntents.AllUnprivileged ||| GatewayIntents.MessageContent))

    services
        .AddSingleton<DiscordSocketClient>(
            Func<IServiceProvider, DiscordSocketClient>(fun _ -> new DiscordSocketClient(socketConfig))
        )
        .AddSingleton<IHostedService>(
            Func<IServiceProvider, IHostedService>(fun provider ->
                BotService.hostedService
                    (provider.GetRequiredService<DiscordSocketClient>())
                    (provider.GetRequiredService<IConfiguration>())
                    (provider.GetRequiredService<ILoggerFactory>()))
        )
        .AddRouting()
        .AddOxpecker()
    |> ignore

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder.UseRouting().Use(errorHandler).UseOxpecker(getMainRouter).Run(notFoundHandler)

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    let app = builder.Build()
    configureApp app
    app.Run()
    0
