using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TradeDocsApi.Data;
using TradeDocsApi.Services;

namespace TradeDocsApi.Controllers
{
    // ==========================================
    // 1. КОНТРОЛЕР ДЛЯ 1С (З ЗАПИСОМ ЛОГІВ)
    // ==========================================
    [ApiController]
    [Route("api/sync")]
    public class SyncController : ControllerBase
    {
        private readonly ISyncService _syncService;
        private readonly AppDbContext _db;
        private readonly ILogger<SyncController> _logger;

        public SyncController(ISyncService syncService, AppDbContext db, ILogger<SyncController> logger)
        {
            _syncService = syncService;
            _db = db;
            _logger = logger;
        }

        [HttpPost("push")]
        public async Task<IActionResult> PushData([FromBody] SyncPayload request)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int recordCount = 0;

            try
            {
                // Швидкий підрахунок кількості об'єктів у масиві JSON
                using (var doc = JsonDocument.Parse(request.Payload))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array) recordCount = doc.RootElement.GetArrayLength();
                }

                await _syncService.ProcessPayloadAsync(request.DataType, request.Payload);
                watch.Stop();

                // Запис успішного обміну в БД
                _db.SyncHistories.Add(new SyncHistory
                {
                    Timestamp = DateTime.UtcNow,
                    DataType = request.DataType,
                    RecordCount = recordCount,
                    IsSuccess = true,
                    ExecutionTimeMs = watch.ElapsedMilliseconds
                });
                await _db.SaveChangesAsync();

                _logger.LogInformation("Пакет {DataType} ({Count} шт) успішно оброблено", request.DataType, recordCount);
                return Ok(new { Message = $"Успішно оброблено пакет типу {request.DataType} ({recordCount} записів)" });
            }
            catch (Exception ex)
            {
                watch.Stop();
                // Запис помилки в БД
                _db.SyncHistories.Add(new SyncHistory
                {
                    Timestamp = DateTime.UtcNow,
                    DataType = request.DataType,
                    RecordCount = recordCount,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ExecutionTimeMs = watch.ElapsedMilliseconds
                });
                await _db.SaveChangesAsync();

                _logger.LogError(ex, "Помилка обробки пакету {DataType}", request.DataType);
                return BadRequest(new { Error = ex.Message });
            }
        }
    }

    // ==========================================
    // 2. АВТОРИЗАЦІЯ ДЛЯ ДАШБОРДУ
    // ==========================================
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request.Username == "admin" && request.Password == "admin123")
            {
                var claims = new List<Claim> { new Claim(ClaimTypes.Name, request.Username) };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                return Ok();
            }
            return Unauthorized(new { Error = "Невірний логін або пароль" });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }
    }
    public class LoginRequest { public string Username { get; set; } = ""; public string Password { get; set; } = ""; }

    // ==========================================
    // 3. КОНТРОЛЕР ДЛЯ ДАШБОРДУ 
    // ==========================================
    [Authorize]
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db) { _db = db; }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            return Ok(new
            {
                Products = await _db.Products.CountAsync(),
                Shops = await _db.Shops.CountAsync(),
                Orders = await _db.Orders.CountAsync(),
                Remains = await _db.Remains.CountAsync()
            });
        }

        // Дані для Графіку Активності (сума пакетів за останні 7 днів)
        [HttpGet("activity")]
        public async Task<IActionResult> GetActivityData()
        {
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
            var history = await _db.SyncHistories
                .Where(x => x.Timestamp >= sevenDaysAgo && x.IsSuccess)
                .GroupBy(x => x.Timestamp.Date)
                .Select(g => new { Date = g.Key, TotalRecords = g.Sum(x => x.RecordCount) })
                .ToListAsync();

            return Ok(history);
        }

        // Дані для вкладки Журнал
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs(int page = 1, int pageSize = 15)
        {
            var total = await _db.SyncHistories.CountAsync();
            var items = await _db.SyncHistories
                .OrderByDescending(x => x.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return Ok(new { Total = total, Items = items });
        }

        // Інші довідники
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(int page = 1, int pageSize = 15)
        {
            return Ok(new { Total = await _db.Products.CountAsync(), Items = await _db.Products.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(int page = 1, int pageSize = 15)
        {
            return Ok(new { Total = await _db.Orders.CountAsync(), Items = await _db.Orders.Include(o => o.Items).OrderByDescending(o => o.Date).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
        }

        [HttpGet("shops")]
        public async Task<IActionResult> GetShops(int page = 1, int pageSize = 15)
        {
            return Ok(new { Total = await _db.Shops.CountAsync(), Items = await _db.Shops.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
        }

        [HttpGet("counterparties")]
        public async Task<IActionResult> GetCounterparties(int page = 1, int pageSize = 15)
        {
            return Ok(new { Total = await _db.Counterparties.CountAsync(), Items = await _db.Counterparties.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
        }
    }
}