using LiveFuelMap.DAL.Entities;
using LiveFuelMap.DAL.Enums;
using Microsoft.EntityFrameworkCore;

namespace LiveFuelMap.DAL.Persistence;

public sealed class LiveFuelMapDbContext(DbContextOptions<LiveFuelMapDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Fuel> Fuels => Set<Fuel>();
    public DbSet<FuelPrice> FuelPrices => Set<FuelPrice>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<ParserRun> ParserRuns => Set<ParserRun>();
    public DbSet<ApplicationLog> ApplicationLogs => Set<ApplicationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var currentTimestampSql = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? "CURRENT_TIMESTAMP"
            : "CURRENT_TIMESTAMP(6)";

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.Email).HasMaxLength(255).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100);
            entity.Property(x => x.Nickname).HasMaxLength(32);
            entity.Property(x => x.NormalizedNickname).HasMaxLength(32);
            entity.Property(x => x.ProfileImageUrl).HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.EmailConfirmationTokenHash).HasMaxLength(255);
            entity.Property(x => x.PasswordResetTokenHash).HasMaxLength(255);
            entity.Property(x => x.RefreshTokenHash).HasMaxLength(255);
            entity.Property(x => x.TokenVersion).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.NormalizedNickname).IsUnique();
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
        });

        modelBuilder.Entity<Station>(entity =>
        {
            entity.ToTable("stations");
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired();
            entity.Property(x => x.NormalizedKey).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(500).IsRequired();
            entity.Property(x => x.City).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Latitude).HasPrecision(10, 6);
            entity.Property(x => x.Longitude).HasPrecision(10, 6);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.Property(x => x.WebsiteUrl).HasMaxLength(500);
            entity.Property(x => x.PhotoUrls).HasMaxLength(2000);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasIndex(x => new { x.NormalizedKey, x.City }).IsUnique();
            entity.HasIndex(x => x.City);
        });

        modelBuilder.Entity<Fuel>(entity =>
        {
            entity.ToTable("fuels");
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FuelPrice>(entity =>
        {
            entity.ToTable("fuel_prices");
            entity.Property(x => x.Price).HasPrecision(6, 2);
            entity.Property(x => x.Date).HasColumnType("date");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasOne(x => x.Station).WithMany(x => x.FuelPrices).HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Fuel).WithMany(x => x.FuelPrices).HasForeignKey(x => x.FuelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Source).WithMany(x => x.FuelPrices).HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.StationId, x.FuelId, x.Date }).IsUnique();
            entity.HasIndex(x => new { x.FuelId, x.Date });
            entity.HasIndex(x => new { x.StationId, x.FuelId, x.Date });
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.Property(x => x.City).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasOne(x => x.User).WithMany(x => x.Subscriptions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Fuel).WithMany(x => x.Subscriptions).HasForeignKey(x => x.FuelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.UserId, x.FuelId, x.City, x.Frequency }).IsUnique();
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("comments");
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Rating).HasDefaultValue(5);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasOne(x => x.User).WithMany(x => x.Comments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Station).WithMany(x => x.Comments).HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Fuel).WithMany(x => x.Comments).HasForeignKey(x => x.FuelId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApiToken>(entity =>
        {
            entity.ToTable("api_tokens");
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Scopes).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasOne(x => x.CreatedByUser).WithMany(x => x.CreatedApiTokens).HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.RevokedAt);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.Property(x => x.SessionId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UserMessage).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.BotResponse).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Intent).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasOne(x => x.User).WithMany(x => x.ChatMessages).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.SessionId, x.CreatedAt });
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.ToTable("data_sources");
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ParserRun>(entity =>
        {
            entity.ToTable("parser_runs");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasOne(x => x.Source).WithMany(x => x.ParserRuns).HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.SourceId, x.StartedAt });
        });

        modelBuilder.Entity<ApplicationLog>(entity =>
        {
            entity.ToTable("application_logs");
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Exception).HasMaxLength(4000);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql(currentTimestampSql);
            entity.HasIndex(x => new { x.Category, x.CreatedAt });
        });

        SeedReferenceData(modelBuilder);
    }

    private static void SeedReferenceData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Fuel>().HasData(
            new Fuel { Id = 1, Code = "a95plus", Name = "А 95+", SortOrder = 1 },
            new Fuel { Id = 2, Code = "a95", Name = "А 95", SortOrder = 2 },
            new Fuel { Id = 3, Code = "a92", Name = "А 92", SortOrder = 3 },
            new Fuel { Id = 4, Code = "diesel", Name = "ДП", SortOrder = 4 },
            new Fuel { Id = 5, Code = "gas", Name = "Газ", SortOrder = 5 });

        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Station>().HasData(
            new Station { Id = 1, Name = "AMIC", NormalizedKey = "amic", Address = "проспект Героїв Харкова, 142A", City = "Харків", Latitude = 49.966850m, Longitude = 36.317010m, ImageUrl = "https://th.bing.com/th/id/OIP.3P7fCEmHgzRbvLL5VGI9WwHaFP?rs=1&pid=ImgDetMain", WebsiteUrl = "https://amicenergy.com.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 2, Name = "Marshal", NormalizedKey = "marshal", Address = "вулиця Григорія Сковороди, 85", City = "Харків", Latitude = 50.012210m, Longitude = 36.255580m, ImageUrl = "https://www.azski.com.ua/images/networks/marshal.jpg", WebsiteUrl = "https://www.azski.com.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 3, Name = "Ovis", NormalizedKey = "ovis", Address = "вулиця Клочківська, 98А", City = "Харків", Latitude = 50.005090m, Longitude = 36.218900m, ImageUrl = "https://codeit4.life/app/uploads/2023/03/ovis.png", WebsiteUrl = "https://ovis.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 4, Name = "Rodnik", NormalizedKey = "rodnik", Address = "Аерокосмічний проспект, 223", City = "Харків", Latitude = 49.884170m, Longitude = 36.291900m, ImageUrl = "https://th.bing.com/th/id/OIP.HVtgS-2xJha8xLwQ--v4PwAAAA?rs=1&pid=ImgDetMain", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 5, Name = "SUN OIL", NormalizedKey = "sun-oil", Address = "вулиця Некрасова", City = "Харків", Latitude = 49.947570m, Longitude = 36.186600m, ImageUrl = "https://mir-s3-cdn-cf.behance.net/project_modules/fs/ea1e0a17798911.563603a213dcf.jpg", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 6, Name = "Shell", NormalizedKey = "shell", Address = "Av. Zhukov, проспект Петра Григоренка", City = "Харків", Latitude = 49.945000m, Longitude = 36.313130m, ImageUrl = "https://vsememy.ru/kartinki/wp-content/uploads/2023/03/1643621408_7-papik-pro-p-shell-logotip-7.jpg", WebsiteUrl = "https://www.shell.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 7, Name = "U.GO", NormalizedKey = "ugo", Address = "127а, Аерокосмічний проспект", City = "Харків", Latitude = 49.964610m, Longitude = 36.259940m, ImageUrl = "https://th.bing.com/th/id/OIP.1FVDZYVIATB71QU6VNKCZAHaHa?rs=1&pid=ImgDetMain", WebsiteUrl = "https://ugo.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 8, Name = "WOG", NormalizedKey = "wog", Address = "вулиця Шевченка, 41", City = "Харків", Latitude = 49.996090m, Longitude = 36.250020m, ImageUrl = "https://th.bing.com/th/id/OIP.NY1gJdDiJy6WD6lAhRIr6AAAAA?rs=1&pid=ImgDetMain", WebsiteUrl = "https://wog.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 9, Name = "Авіас", NormalizedKey = "avias", Address = "просп. Байрона", City = "Харків", Latitude = 49.945400m, Longitude = 36.332170m, ImageUrl = "https://seeklogo.com/images/A/avias-logo-A0C43F9345-seeklogo.com.png", WebsiteUrl = "https://avias.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 10, Name = "БРСМ-Нафта", NormalizedKey = "brsm-nafta", Address = "вулиця Валентинівська, 2а", City = "Харків", Latitude = 50.021000m, Longitude = 36.319070m, ImageUrl = "https://agrorozvytok.com.ua/images/gotovo-logo-brsm-555.jpg", WebsiteUrl = "https://brsm-nafta.com/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 11, Name = "ОККО", NormalizedKey = "okko", Address = "вулиця Клочківська, 44", City = "Харків", Latitude = 49.997310m, Longitude = 36.227570m, ImageUrl = "https://th.bing.com/th/id/OIP.XWGNsIqGjw97H_jWIOCgRgHaGy?w=177&h=180&c=7&r=0&o=5&pid=1.7", WebsiteUrl = "https://www.okko.ua/", IsActive = true, CreatedAt = createdAt },
            new Station { Id = 12, Name = "Укрнафта", NormalizedKey = "ukrnafta", Address = "вулиця Академіка Павлова,", City = "Харків", Latitude = 49.989690m, Longitude = 36.287720m, ImageUrl = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcS14Iy__vrlMNJgM7WMJJaWXCALSMzIz5ADHg&s", WebsiteUrl = "https://www.ukrnafta.com/", IsActive = true, CreatedAt = createdAt });

        modelBuilder.Entity<DataSource>().HasData(
            new DataSource { Id = 1, Name = "Minfin Kharkiv", Url = "https://index.minfin.com.ua/ua/markets/fuel/reg/harkovskaya/", Type = DataSourceType.Html, Enabled = true },
            new DataSource { Id = 2, Name = "Open Fuel JSON", Url = "https://example.com/fuel-prices.json", Type = DataSourceType.Json, Enabled = false });
    }
}
