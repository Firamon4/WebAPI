using Microsoft.EntityFrameworkCore;
using TradeDocsApi.Data;
using TradeDocsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Налаштування Бази Даних 
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=TradeDocsDB;Trusted_Connection=True;TrustServerCertificate=True;"));

// 2. Реєстрація сервісів (Dependency Injection)
builder.Services.AddScoped<ISyncService, SyncService>();

// 3. Налаштування MVC та контролерів
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. Автоматичне створення БД при першому запуску
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// 5. Middleware: Налаштування статичних файлів для панелі керування (wwwroot/index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

// 6. Middleware: Захист API для 1С за допомогою X-Api-Key
app.Use(async (context, next) =>
{
    // Захищаємо тільки маршрути синхронізації
    if (context.Request.Path.StartsWithSegments("/api/sync"))
    {
        const string API_KEY = "123456789"; 
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey) || !API_KEY.Equals(extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Error = "Unauthorized. Invalid API Key." });
            return;
        }
    }
    await next.Invoke();
});

app.MapControllers();

app.Run();