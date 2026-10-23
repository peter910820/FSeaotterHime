module FSeaotterHime.Discord.HttpHandlers

open System
open System.IO
open System.Text
open System.Xml.Linq
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Oxpecker
open type Microsoft.AspNetCore.Http.TypedResults
open FSeaotterHime.Database

let private atomName name =
    XName.Get(name, "http://www.w3.org/2005/Atom")

let private ytName name =
    XName.Get(name, "http://www.youtube.com/xml/schemas/2015")

type YoutubeVideo =
    { VideoId: string
      ChannelId: string
      Title: string
      Url: string }

let private elementValue (element: XElement) name =
    match element.Element(name) with
    | null -> None
    | child -> Some child.Value

let private alternateUrl (entry: XElement) (videoId: string) =
    entry.Elements(atomName "link")
    |> Seq.tryPick (fun link ->
        let rel = link.Attribute(XName.Get "rel")
        let href = link.Attribute(XName.Get "href")

        if rel <> null && href <> null && rel.Value = "alternate" then
            Some href.Value
        else
            None)
    |> Option.defaultValue $"https://www.youtube.com/watch?v={videoId}"

let private tryGetYoutubeChannelId (topic: string) =
    match Uri.TryCreate(topic, UriKind.Absolute) with
    | true, uri ->
        uri.Query.TrimStart('?').Split('&')
        |> Array.tryPick (fun parameter ->
            match parameter.Split('=', 2) with
            | [| "channel_id"; channelId |] when not (String.IsNullOrEmpty channelId) ->
                Some(Uri.UnescapeDataString(channelId))
            | _ -> None)
    | false, _ -> None

let parseYoutubeVideo (body: string) =
    let doc = XDocument.Parse(body)

    match doc.Root with
    | null -> None
    | root ->
        match root.Element(atomName "entry") with
        | null -> None
        | entry ->
            match elementValue entry (ytName "videoId"),
                  elementValue entry (ytName "channelId"),
                  elementValue entry (atomName "title") with
            | Some videoId, Some channelId, Some title ->
                Some
                    { VideoId = videoId
                      ChannelId = channelId
                      Title = title
                      Url = alternateUrl entry videoId }
            | _ -> None

let youtubeVerification : EndpointHandler =
    fun (ctx: HttpContext) ->
        task {
            let logger = ctx.GetLogger()
            let query = ctx.Request.Query
            let mode = string query["hub.mode"]
            let topic = string query["hub.topic"]
            let challenge = string query["hub.challenge"]
            let verifyToken = string query["hub.verify_token"]
            let leaseSeconds = string query["hub.lease_seconds"]
            let config = ctx.GetService<IConfiguration>()
            let expectedToken = config["PubSubHubbub:VerifyToken"]
            let discordChannelId = config["Discord:YoutubeSubscriptionChannelId"]

            let deny (reason: string) =
                logger.LogWarning("拒絕 YouTube 訂閱驗證：{Reason}", reason)
                ctx.Write <| StatusCode StatusCodes.Status404NotFound

            if String.IsNullOrEmpty expectedToken || verifyToken <> expectedToken then
                return! deny "verify_token 不符"
            elif String.IsNullOrEmpty challenge then
                return! deny "缺少 hub.challenge"
            elif mode <> "subscribe" && mode <> "unsubscribe" then
                return! deny "hub.mode 不正確"
            elif mode = "subscribe" && String.IsNullOrEmpty discordChannelId then
                return! deny "Discord:YoutubeSubscriptionChannelId 未設定"
            else
                if mode = "subscribe" then
                    match tryGetYoutubeChannelId topic, Int32.TryParse leaseSeconds with
                    | Some channelId, (true, seconds) ->
                        do!
                            saveYoutubeSubscription
                                ctx.RequestServices
                                { Id = channelId
                                  ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(float seconds)
                                  DiscordChannelId = discordChannelId }
                    | None, _ -> return! deny "hub.topic 缺少 channel_id"
                    | _, _ -> return! deny "hub.lease_seconds 不正確"

                logger.LogInformation("確認 YouTube {Mode} {Topic}", mode, topic)
                return! ctx.Write <| Text(challenge, "text/plain")
        }

let youtubeNotification : EndpointHandler =
    fun (ctx: HttpContext) ->
        task {
            use reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, true, 1024, true)
            let! body = reader.ReadToEndAsync()
            let logger = ctx.GetLogger()

            match parseYoutubeVideo body with
            | None -> logger.LogWarning("YouTube 推播沒有影片")
            | Some video ->
                logger.LogInformation(
                    "YouTube 影片 {VideoId} {Title} {ChannelId} {Url}",
                    video.VideoId,
                    video.Title,
                    video.ChannelId,
                    video.Url
                )
        }
