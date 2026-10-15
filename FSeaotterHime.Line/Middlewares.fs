module FSeaotterHime.Line.Middlewares

open System
open System.Security.Cryptography
open System.Text
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Oxpecker

let private isSignatureValid (signature: string) (computedHash: byte[]) =
    let computedSignature = Convert.ToBase64String(computedHash)
    CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(computedSignature),
        Encoding.UTF8.GetBytes(signature)
    )

let private computeBodyHashAsync (secret: string) (request: HttpRequest) =
    task {
        request.EnableBuffering()
        let keyBytes = Encoding.UTF8.GetBytes(secret)
        let! hash = HMACSHA256.HashDataAsync(keyBytes, request.Body)
        request.Body.Position <- 0L
        return hash
    }

let verifySignatureMiddleware : EndpointMiddleware =
    fun (next: EndpointHandler) (ctx: HttpContext) ->
        task {
            let secret = ctx.GetService<IConfiguration>()["LineBot:ChannelSecret"]

            match ctx.TryGetHeaderValue "x-line-signature" with
            | None ->
                ctx.SetStatusCode StatusCodes.Status401Unauthorized
                return! ctx.WriteText "Missing x-line-signature header"
            | Some signature ->
                let! computedHash = computeBodyHashAsync secret ctx.Request

                if isSignatureValid signature computedHash then
                    return! next ctx
                else
                    ctx.SetStatusCode StatusCodes.Status401Unauthorized
                    return! ctx.WriteText "Invalid signature"
        }
