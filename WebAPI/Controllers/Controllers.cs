using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeDocsApi.Data;
using TradeDocsApi.Services;

namespace TradeDocsApi.Controllers
{
    // ==========================================
    // КОНТРОЛЕР ДЛЯ 1С (ПРИЙОМ ДАНИХ)
    // ==========================================
    [ApiController]
    [Route("api/sync")]
    public class SyncController : ControllerBase
    {
        private readonly ISyncService _syncService;

        public SyncController(ISyncService syncService)
        {
            _syncService = syncService;
        }

        [HttpPost("push")]
        public async Task<IActionResult> PushData([FromBody] SyncPayload request)
        {
            try
            {
                await _syncService.ProcessPayloadAsync(request.DataType, request.Payload);
                return Ok(new { Message = $"Успішно оброблено пакет типу {request.DataType}" });
            }
            catch (Exception ex)
            {
                // Запис в лог помилки
                return BadRequest(new { Error = ex.Message });
            }
        }
    }

    // ==========================================
    // КОНТРОЛЕР ДЛЯ ДАШБОРДУ (ВІДДАЧА ДАНИХ)
    // ==========================================
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

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

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts() => Ok(await _db.Products.Take(100).ToListAsync());

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders() => Ok(await _db.Orders.Include(o => o.Items).OrderByDescending(o => o.Date).Take(50).ToListAsync());
    }
}