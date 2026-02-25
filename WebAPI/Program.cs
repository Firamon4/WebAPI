using Microsoft.EntityFrameworkCore;
using TradeDocsApi.Data;
using TradeDocsApi.Services;
using Serilog;
using Microsoft.AspNetCore.Authentication.Cookies;

// 1. Налаштування Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/tradedocs-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // 2. База даних MS SQL Server 
    string connectionString = "Server=localhost\\SQLEXPRESS;Database=TradeDocsDB;Trusted_Connection=True;TrustServerCertificate=True;";

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));

    // 3. Налаштування авторизації для панелі керування (Cookie)
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "TradeDocsAuth";
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });
    builder.Services.AddAuthorization();

    // 4. Реєстрація сервісів
    builder.Services.AddScoped<ISyncService, SyncService>();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // 5. Автоматичне створення БД при першому запуску
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    // 6. Роздача статичних файлів (наш index.html)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseAuthentication();
    app.UseAuthorization();

    // 7. Middleware: Захист API для 1С (X-Api-Key)
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/sync"))
        {
            const string API_KEY = "123456789";
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey) || !API_KEY.Equals(extractedApiKey))
            {
                Log.Warning("Спроба неавторизованого доступу до API з IP {IP}", context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { Error = "Unauthorized. Invalid API Key." });
                return;
            }
        }
        await next.Invoke();
    });

    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Критична помилка при запуску програми");
}
finally
{
    Log.CloseAndFlush();
}