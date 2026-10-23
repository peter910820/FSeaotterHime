module FSeaotterHime.Discord.HttpHandlers

open System
open System.IO
open System.Text
open System.Xml.Linq
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Oxpecker
open Oxpecker.ViewEngine
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

let private youtubeSubscriptionsTable (subscriptions: YoutubeSubscription list) =
    table(class' = "subscriptions") {
        thead() {
            tr() {
                th() { "YouTube 頻道 ID" }
                th() { "到期時間" }
                th() { "Discord 頻道 ID" }
            }
        }
        tbody() {
            if List.isEmpty subscriptions then
                tr() {
                    td().attr("colspan", "3") { "尚無訂閱資料。" }
                }
            else
                for subscription in subscriptions do
                    tr() {
                        td() { subscription.Id }
                        td() { subscription.ExpiresAt.ToString("O") }
                        td() { subscription.DiscordChannelId }
                    }
        }
    }

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

let youtubeSubscriptionsPage : EndpointHandler =
    fun (ctx: HttpContext) ->
        task {
            let! subscriptions = queryYoutubeSubscriptions ctx.RequestServices

            let page =
                html().attr("lang", "zh-Hant") {
                    head() {
                        meta(charset = "utf-8")
                        title() { "YouTube 訂閱資料" }
                        style() {
                            raw
                                """
body {
    max-width: 960px;
    margin: 48px auto;
    padding: 0 24px;
    background: #f8fafc;
    color: #1e293b;
    font-family: system-ui, sans-serif;
}

h1 {
    margin-bottom: 24px;
}

.subscriptions {
    width: 100%;
    border-collapse: collapse;
    overflow: hidden;
    background: white;
    border-radius: 8px;
    box-shadow: 0 1px 3px #0000001a;
}

.subscriptions th,
.subscriptions td {
    padding: 12px 16px;
    text-align: left;
    border-bottom: 1px solid #e2e8f0;
}

.subscriptions th {
    background: #0f766e;
    color: white;
}

.subscriptions tbody tr:last-child td {
    border-bottom: 0;
}

.subscriptions tbody tr:hover {
    background: #f0fdfa;
}
"""
                        }
                    }
                    body() {
                        h1() { "YouTube 訂閱資料" }
                        youtubeSubscriptionsTable subscriptions
                    }
                }

            return! ctx.Write <| Text(page |> Render.toHtmlDocString, "text/html; charset=utf-8")
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
