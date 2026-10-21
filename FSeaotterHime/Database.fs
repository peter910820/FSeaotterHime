module FSeaotterHime.Database

open System
open System.ComponentModel.DataAnnotations
open System.ComponentModel.DataAnnotations.Schema
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Storage
open Microsoft.Extensions.DependencyInjection

[<CLIMutable>]
[<Table("youtube_subscription")>]
type YoutubeSubscription =
    { [<Key>]
      [<Column("id")>]
      Id: string
      [<Required>]
      [<Column("expires_at")>]
      ExpiresAt: DateTimeOffset
      [<Required>]
      [<Column("discord_channel_id")>]
      DiscordChannelId: string }

type AppDbContext(options: DbContextOptions<AppDbContext>) =
    inherit DbContext(options)

    member val YoutubeSubscriptions: DbSet<YoutubeSubscription> = Unchecked.defaultof<_> with get, set

let addAppDbContext (services: IServiceCollection) (connectionString: string) =
    services.AddDbContext<AppDbContext>(fun options -> options.UseNpgsql(connectionString) |> ignore)
    |> ignore

let ensureCreated (services: IServiceProvider) =
    use scope = services.CreateScope()
    let db = scope.ServiceProvider.GetRequiredService<AppDbContext>()
    let creator = db.Database.GetService<IRelationalDatabaseCreator>()

    if not (creator.Exists()) then
        creator.Create()

    if not (creator.HasTables()) then
        creator.CreateTables()

let querySubscriptionsExpiringWithinOneDay (services: IServiceProvider) =
    task {
        use scope = services.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<AppDbContext>()
        let deadline = DateTimeOffset.UtcNow.AddDays(1.0)

        let! rows =
            (query {
                for subscription in db.YoutubeSubscriptions do
                    where (subscription.ExpiresAt < deadline)
                    select subscription
            })
                .ToListAsync()

        return rows |> List.ofSeq
    }

let renewSubscriptionExpiresAt (services: IServiceProvider) (channelId: string) (expiresAt: DateTimeOffset) =
    task {
        use scope = services.CreateScope()
        let db = scope.ServiceProvider.GetRequiredService<AppDbContext>()

        let! rows =
            (query {
                for subscription in db.YoutubeSubscriptions do
                    where (subscription.Id = channelId)
                    select subscription
            })
                .ToListAsync()

        match Seq.tryHead rows with
        | None -> return false
        | Some subscription ->
            db.Entry(subscription).CurrentValues.SetValues({ subscription with ExpiresAt = expiresAt })
            let! _ = db.SaveChangesAsync()
            return true
    }
