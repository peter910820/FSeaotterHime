module FSeaotterHime.Discord.Handlers

open System
open System.Threading.Tasks
open Discord
open Discord.WebSocket
open Microsoft.Extensions.Logging
open FSeaotterHime.Fortune

let private replyText (message: SocketMessage) =
    if message.Author.IsBot || String.IsNullOrWhiteSpace message.Content then
        None
    else
        Some message.Content

let private echo (message: SocketMessage) =
    match replyText message with
    | None -> Task.CompletedTask
    | Some text -> message.Channel.SendMessageAsync(text) :> Task

let private avatarUrl (user: SocketUser) =
    user.GetAvatarUrl()
    |> Option.ofObj
    |> Option.defaultWith user.GetDefaultAvatarUrl

let private fortuneEmbed (user: SocketUser) =
    EmbedBuilder()
        .WithTitle("今日運勢")
        .WithDescription(getFortunate ())
        .WithThumbnailUrl(avatarUrl user)
        .WithColor(Color.Gold)
        .Build()

let private fortune (command: SocketSlashCommand) =
    command.RespondAsync(embed = fortuneEmbed command.User)

let private onSlashCommand (command: SocketSlashCommand) =
    match command.CommandName with
    | "運勢" -> fortune command
    | _ -> Task.CompletedTask

let private registerCommands (client: DiscordSocketClient) (logger: ILogger) =
    task {
        let command = SlashCommandBuilder().WithName("運勢").WithDescription("抽今天的運勢").Build()

        let commands: ApplicationCommandProperties[] = [| command |]

        for guild in client.Guilds do
            let! _ = guild.BulkOverwriteApplicationCommandAsync(commands)
            logger.LogInformation("Registered commands in {Guild}", guild.Name)
    }
    :> Task

let register (client: DiscordSocketClient) (logger: ILogger) =
    client.add_Ready (
        Func<Task>(fun () ->
            task {
                logger.LogInformation("Logged in as {User}", client.CurrentUser)
                do! registerCommands client logger
            }
            :> Task)
    )

    client.add_MessageReceived (Func<SocketMessage, Task>(echo))

    client.add_SlashCommandExecuted (
        Func<SocketSlashCommand, Task>(fun command ->
            task {
                try
                    do! onSlashCommand command
                with ex ->
                    logger.LogError(ex, "slash command failed")
            }
            |> ignore

            Task.CompletedTask)
    )
