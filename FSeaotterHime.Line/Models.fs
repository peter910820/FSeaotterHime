module FSeaotterHime.Line.Models

type LineSource = { Type: string; UserId: string option }

type LineMessage =
    { Id: string
      Type: string
      Text: string option }

type LineEvent =
    { Type: string
      ReplyToken: string option
      Source: LineSource
      Message: LineMessage option }

type LineWebhookPayload =
    { Destination: string
      Events: LineEvent list }
