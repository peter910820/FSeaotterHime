module FSeaotterHime.Fortune

let private fortuneTable =
    [ 90, "你再抽一次🤡"
      85, "仙草吉👑"
      75, "大吉⭐"
      60, "中吉🌟"
      40, "小吉👌"
      20, "末吉🙏"
      5, "凶💀" ]

let getFortunate () =
    let randNumber = System.Random.Shared.Next(100)

    fortuneTable
    |> List.tryFind (fun (threshold, _) -> randNumber > threshold)
    |> Option.map snd
    |> Option.defaultValue "大凶☠️"
