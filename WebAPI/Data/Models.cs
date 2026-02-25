using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace TradeDocsApi.Data
{
    // ==========================================
    // 1. КОНТЕКСТ БАЗИ ДАНИХ (EF CORE)
    // ==========================================
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Counterparty> Counterparties { get; set; }
        public DbSet<Shop> Shops { get; set; }
        public DbSet<Worker> Workers { get; set; }

        public DbSet<Specification> Specifications { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<ReturnAndComing> ReturnsAndComings { get; set; }

        public DbSet<Remain> Remains { get; set; }
        public DbSet<Price> Prices { get; set; }

        // НОВА ТАБЛИЦЯ: Журнал обмінів з 1С
        public DbSet<SyncHistory> SyncHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Виправляємо попередження про decimal: глобально задаємо формат 18,4
            foreach (var property in builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,4)");
            }

            builder.Entity<Remain>().HasKey(r => new { r.Subdivision, r.ProductUid });
            builder.Entity<Price>().HasKey(p => new { p.PriceTypeRef, p.ProductRef });

            builder.Entity<Specification>().HasMany(s => s.Items).WithOne().HasForeignKey(i => i.ParentRef).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<Order>().HasMany(o => o.Items).WithOne().HasForeignKey(i => i.ParentRef).OnDelete(DeleteBehavior.Cascade);
            builder.Entity<ReturnAndComing>().HasMany(r => r.Items).WithOne().HasForeignKey(i => i.ParentRef).OnDelete(DeleteBehavior.Cascade);
        }
    }

    // ==========================================
    // 2. СУТНОСТІ БАЗИ ДАНИХ (ENTITIES)
    // ==========================================

    // СУТНІСТЬ ЛОГІВ
    public class SyncHistory
    {
        [Key] public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string DataType { get; set; } = "";
        public int RecordCount { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public long ExecutionTimeMs { get; set; }
    }

    public class Product { [Key] public string Ref { get; set; } = ""; public string? Code { get; set; } public string? Name { get; set; } public string? Articul { get; set; } public string? Barcode { get; set; } public bool IsFolder { get; set; } public bool IsActual { get; set; } public bool IsDeleted { get; set; } public string? ParentRef { get; set; } }
    public class Counterparty { [Key] public string Ref { get; set; } = ""; public string? Name { get; set; } public string? Code { get; set; } public string? TaxId { get; set; } public bool IsDeleted { get; set; } }
    public class Shop { [Key] public string Ref { get; set; } = ""; public string? Name { get; set; } public string? ShopNumber { get; set; } public bool IsDeleted { get; set; } public string? PriceType { get; set; } public string? Subdivision { get; set; } public string? SubdivisionName { get; set; } }
    public class Worker { [Key] public string Ref { get; set; } = ""; public string? WorkerName { get; set; } public string? Subdivision { get; set; } public string? SubdivisionName { get; set; } public bool IsActual { get; set; } public string? Position { get; set; } public string? PositionName { get; set; } }

    public class Specification { [Key] public string Ref { get; set; } = ""; public string? Number { get; set; } public DateTime Date { get; set; } public string? CounterpartyRef { get; set; } public bool IsDeleted { get; set; } public bool IsApproved { get; set; } public string? PriceType { get; set; } public List<SpecificationItem> Items { get; set; } = new(); }
    public class SpecificationItem { public int Id { get; set; } public string ParentRef { get; set; } = ""; public string? ProductRef { get; set; } public decimal Price { get; set; } public string? Unit { get; set; } public string? UnitName { get; set; } }

    public class Order { [Key] public string Ref { get; set; } = ""; public string? Number { get; set; } public DateTime Date { get; set; } public string? CounterpartyUid { get; set; } public bool IsDeleted { get; set; } public bool IsApproved { get; set; } public List<OrderItem> Items { get; set; } = new(); }
    public class OrderItem { public int Id { get; set; } public string ParentRef { get; set; } = ""; public string? ProductRef { get; set; } public decimal Price { get; set; } public decimal Count { get; set; } public decimal CountFact { get; set; } public string? Unit { get; set; } public string? UnitName { get; set; } }

    public class ReturnAndComing { [Key] public string Ref { get; set; } = ""; public string? Number { get; set; } public string? DocType { get; set; } public DateTime Date { get; set; } public string? SenderUid { get; set; } public string? RecipientUid { get; set; } public string? OrderUid { get; set; } public bool IsDeleted { get; set; } public bool IsApproved { get; set; } public List<ReturnAndComingItem> Items { get; set; } = new(); }
    public class ReturnAndComingItem { public int Id { get; set; } public string ParentRef { get; set; } = ""; public string? ProductRef { get; set; } public string? ProductName { get; set; } public decimal Price { get; set; } public decimal Count { get; set; } public decimal CountReceived { get; set; } public decimal CountAccepted { get; set; } public decimal CountInOrder { get; set; } public string? Unit { get; set; } public string? UnitName { get; set; } }

    public class Remain { public string Subdivision { get; set; } = ""; public string? SubdivisionName { get; set; } public string ProductUid { get; set; } = ""; public decimal Quantity { get; set; } }
    public class Price { public string PriceTypeRef { get; set; } = ""; public string ProductRef { get; set; } = ""; public decimal PriceValue { get; set; } public string? Currency { get; set; } }

    // ==========================================
    // 3. DTO (ДЛЯ ДЕСЕРІАЛІЗАЦІЇ З 1С)
    // ==========================================
    public class SyncPayload { public string Source { get; set; } = ""; public string Target { get; set; } = ""; public string DataType { get; set; } = ""; public string Payload { get; set; } = ""; }
    public class BaseDto { [JsonPropertyName("Ref")] public string Ref { get; set; } = ""; [JsonPropertyName("isPhysicallyDeleted")] public bool? IsPhysicallyDeleted { get; set; } }

    public class ProductDto : BaseDto { public string? Code { get; set; } public string? Name { get; set; } public string? Articul { get; set; } public string? Barcode { get; set; } public bool IsFolder { get; set; } public bool IsActual { get; set; } public bool IsDeleted { get; set; } public string? ParentRef { get; set; } }
    public class CounterpartyDto : BaseDto { public string? Name { get; set; } public string? Code { get; set; } public string? TaxId { get; set; } public bool IsDeleted { get; set; } }
    public class ShopDto : BaseDto { public string? Name { get; set; } public string? ShopNumber { get; set; } public bool IsDeleted { get; set; } public string? PriceType { get; set; } public string? Subdivision { get; set; } public string? SubdivisionName { get; set; } }
    public class WorkerDto : BaseDto { public string? WorkerName { get; set; } public string? Subdivision { get; set; } public string? SubdivisionName { get; set; } public bool IsActual { get; set; } public string? Position { get; set; } public string? PositionName { get; set; } }

    public class SpecificationDto : BaseDto { public string? Number { get; set; } public DateTime Date { get; set; } public string? CounterpartyRef { get; set; } public bool IsDeleted { get; set; } public bool IsApproved { get; set; } public string? PriceType { get; set; } public List<SpecificationItemDto> Items { get; set; } = new(); }
    public class SpecificationItemDto { [JsonPropertyName("ParetRef")] public string? ParetRef { get; set; } public string? ProductRef { get; set; } public decimal Price { get; set; } public string? Unit { get; set; } public string? UnitName { get; set; } }

    public class OrderDto : BaseDto { public string? Number { get; set; } public DateTime Date { get; set; } public string? CounterpartyUid { get; set; } public bool IsDeleted { get; set; } public bool IsApproved { get; set; } public List<OrderItemDto> Items { get; set; } = new(); }
    public class OrderItemDto { [JsonPropertyName("ParetRef")] public string? ParetRef { get; set; } public string? ProductRef { get; set; } public decimal Price { get; set; } public decimal Count { get; set; } public decimal CountFact { get; set; } public string? Unit { get; set; } public string? UnitName { get; set; } }

    public class ReturnAndComingDto : BaseDto { public string? Number { get; set; } public string? DocType { get; set; } public DateTime Date { get; set; } public string? SenderUid { get; set; } public string? RecipientUid { get; set; } public string? OrderUid { get; set; } public bool IsDeleted { get; set; } public bool IsApproved { get; set; } public List<ReturnAndComingItemDto> Items { get; set; } = new(); }
    public class ReturnAndComingItemDto { [JsonPropertyName("ParetRef")] public string? ParetRef { get; set; } public string? ProductRef { get; set; } public string? ProductName { get; set; } public decimal Price { get; set; } public decimal Count { get; set; } public decimal CountReceived { get; set; } public decimal CountAccepted { get; set; } public decimal CountInOrder { get; set; } public string? Unit { get; set; } public string? UnitName { get; set; } }

    public class RemainDto { [JsonPropertyName("isPhysicallyDeleted")] public bool? IsPhysicallyDeleted { get; set; } public string Subdivision { get; set; } = ""; public string? SubdivisionName { get; set; } public string ProductUid { get; set; } = ""; public decimal Quantity { get; set; } }
    public class PriceDto { [JsonPropertyName("isPhysicallyDeleted")] public bool? IsPhysicallyDeleted { get; set; } public string PriceTypeRef { get; set; } = ""; public string ProductRef { get; set; } = ""; [JsonPropertyName("Price")] public decimal PriceValue { get; set; } public string? Currency { get; set; } }
}