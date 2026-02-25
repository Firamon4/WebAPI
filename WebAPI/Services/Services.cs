using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradeDocsApi.Data;
using EFCore.BulkExtensions;

namespace TradeDocsApi.Services
{
    public interface ISyncService { Task ProcessPayloadAsync(string dataType, string jsonPayload); }

    public class SyncService : ISyncService
    {
        private readonly AppDbContext _db;

        public SyncService(AppDbContext db) { _db = db; }

        public async Task ProcessPayloadAsync(string dataType, string jsonPayload)
        {
            switch (dataType)
            {
                case "Product": await ProcessProducts(jsonPayload); break;
                case "Counterparty": await ProcessCounterparties(jsonPayload); break;
                case "Shop": await ProcessShops(jsonPayload); break;
                case "Worker": await ProcessWorkers(jsonPayload); break;
                case "Specification": await ProcessSpecifications(jsonPayload); break;
                case "Order": await ProcessOrders(jsonPayload); break;
                case "ReturnAndComing": await ProcessReturnsAndComings(jsonPayload); break;
                case "Remain": await ProcessRemainsBulk(jsonPayload); break;
                case "Price": await ProcessPricesBulk(jsonPayload); break;
                default: throw new Exception($"Невідомий тип даних: {dataType}");
            }
        }

        // --- ДОВІДНИКИ ---
        private async Task ProcessProducts(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<ProductDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                var existing = await _db.Products.FindAsync(dto.Ref);
                if (dto.IsPhysicallyDeleted == true) { if (existing != null) _db.Products.Remove(existing); continue; }
                if (existing == null) _db.Products.Add(new Product { Ref = dto.Ref, Code = dto.Code, Name = dto.Name, Articul = dto.Articul, Barcode = dto.Barcode, IsFolder = dto.IsFolder, IsActual = dto.IsActual, IsDeleted = dto.IsDeleted, ParentRef = dto.ParentRef });
                else { existing.Code = dto.Code; existing.Name = dto.Name; existing.Articul = dto.Articul; existing.Barcode = dto.Barcode; existing.IsFolder = dto.IsFolder; existing.IsActual = dto.IsActual; existing.IsDeleted = dto.IsDeleted; existing.ParentRef = dto.ParentRef; }
            }
            await _db.SaveChangesAsync();
        }

        private async Task ProcessCounterparties(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<CounterpartyDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                var existing = await _db.Counterparties.FindAsync(dto.Ref);
                if (dto.IsPhysicallyDeleted == true) { if (existing != null) _db.Counterparties.Remove(existing); continue; }
                if (existing == null) _db.Counterparties.Add(new Counterparty { Ref = dto.Ref, Name = dto.Name, Code = dto.Code, TaxId = dto.TaxId, IsDeleted = dto.IsDeleted });
                else { existing.Name = dto.Name; existing.Code = dto.Code; existing.TaxId = dto.TaxId; existing.IsDeleted = dto.IsDeleted; }
            }
            await _db.SaveChangesAsync();
        }

        private async Task ProcessShops(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<ShopDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                var existing = await _db.Shops.FindAsync(dto.Ref);
                if (dto.IsPhysicallyDeleted == true) { if (existing != null) _db.Shops.Remove(existing); continue; }
                if (existing == null) _db.Shops.Add(new Shop { Ref = dto.Ref, Name = dto.Name, ShopNumber = dto.ShopNumber, IsDeleted = dto.IsDeleted, PriceType = dto.PriceType, Subdivision = dto.Subdivision, SubdivisionName = dto.SubdivisionName });
                else { existing.Name = dto.Name; existing.ShopNumber = dto.ShopNumber; existing.IsDeleted = dto.IsDeleted; existing.PriceType = dto.PriceType; existing.Subdivision = dto.Subdivision; existing.SubdivisionName = dto.SubdivisionName; }
            }
            await _db.SaveChangesAsync();
        }

        private async Task ProcessWorkers(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<WorkerDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                var existing = await _db.Workers.FindAsync(dto.Ref);
                if (dto.IsPhysicallyDeleted == true) { if (existing != null) _db.Workers.Remove(existing); continue; }
                if (existing == null) _db.Workers.Add(new Worker { Ref = dto.Ref, WorkerName = dto.WorkerName, Subdivision = dto.Subdivision, SubdivisionName = dto.SubdivisionName, IsActual = dto.IsActual, Position = dto.Position, PositionName = dto.PositionName });
                else { existing.WorkerName = dto.WorkerName; existing.Subdivision = dto.Subdivision; existing.SubdivisionName = dto.SubdivisionName; existing.IsActual = dto.IsActual; existing.Position = dto.Position; existing.PositionName = dto.PositionName; }
            }
            await _db.SaveChangesAsync();
        }

        // --- ДОКУМЕНТИ ---
        private async Task ProcessOrders(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<OrderDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                var existing = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Ref == dto.Ref);
                if (existing != null) { _db.Orders.Remove(existing); await _db.SaveChangesAsync(); }
                if (dto.IsPhysicallyDeleted == true) continue;
                var newOrder = new Order { Ref = dto.Ref, Number = dto.Number, Date = dto.Date, CounterpartyUid = dto.CounterpartyUid, IsDeleted = dto.IsDeleted, IsApproved = dto.IsApproved };
                newOrder.Items = dto.Items.Select(i => new OrderItem { ParentRef = i.ParetRef ?? dto.Ref, ProductRef = i.ProductRef, Price = i.Price, Count = i.Count, CountFact = i.CountFact, Unit = i.Unit, UnitName = i.UnitName }).ToList();
                _db.Orders.Add(newOrder);
            }
            await _db.SaveChangesAsync();
        }

        private async Task ProcessSpecifications(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<SpecificationDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                var existing = await _db.Specifications.Include(o => o.Items).FirstOrDefaultAsync(o => o.Ref == dto.Ref);
                if (existing != null) { _db.Specifications.Remove(existing); await _db.SaveChangesAsync(); }
                if (dto.IsPhysicallyDeleted == true) continue;
                var newSpec = new Specification { Ref = dto.Ref, Number = dto.Number, Date = dto.Date, CounterpartyRef = dto.CounterpartyRef, IsDeleted = dto.IsDeleted, IsApproved = dto.IsApproved, PriceType = dto.PriceType };
                newSpec.Items = dto.Items.Select(i => new SpecificationItem { ParentRef = i.ParetRef ?? dto.Ref, ProductRef = i.ProductRef, Price = i.Price, Unit = i.Unit, UnitName = i.UnitName }).ToList();
                _db.Specifications.Add(newSpec);
            }
            await _db.SaveChangesAsync();
        }

        private async Task ProcessReturnsAndComings(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<ReturnAndComingDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null) return;
            foreach (var dto in dtos)
            {
                var existing = await _db.ReturnsAndComings.Include(o => o.Items).FirstOrDefaultAsync(o => o.Ref == dto.Ref);
                if (existing != null) { _db.ReturnsAndComings.Remove(existing); await _db.SaveChangesAsync(); }
                if (dto.IsPhysicallyDeleted == true) continue;
                var newDoc = new ReturnAndComing { Ref = dto.Ref, Number = dto.Number, DocType = dto.DocType, Date = dto.Date, SenderUid = dto.SenderUid, RecipientUid = dto.RecipientUid, OrderUid = dto.OrderUid, IsDeleted = dto.IsDeleted, IsApproved = dto.IsApproved };
                newDoc.Items = dto.Items.Select(i => new ReturnAndComingItem { ParentRef = i.ParetRef ?? dto.Ref, ProductRef = i.ProductRef, ProductName = i.ProductName, Price = i.Price, Count = i.Count, CountReceived = i.CountReceived, CountAccepted = i.CountAccepted, CountInOrder = i.CountInOrder, Unit = i.Unit, UnitName = i.UnitName }).ToList();
                _db.ReturnsAndComings.Add(newDoc);
            }
            await _db.SaveChangesAsync();
        }

        // --- РЕГІСТРИ (HIGHLODE BULK INSERT) ---
        private async Task ProcessRemainsBulk(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<RemainDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null || !dtos.Any()) return;

            var toDelete = new List<Remain>();
            var toUpsert = new List<Remain>();

            foreach (var dto in dtos)
            {
                if (string.IsNullOrEmpty(dto.Subdivision) || string.IsNullOrEmpty(dto.ProductUid)) continue;
                var entity = new Remain { Subdivision = dto.Subdivision, SubdivisionName = dto.SubdivisionName, ProductUid = dto.ProductUid, Quantity = dto.Quantity };
                if (dto.IsPhysicallyDeleted == true || dto.Quantity == 0) toDelete.Add(entity); else toUpsert.Add(entity);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                if (toDelete.Any()) await _db.BulkDeleteAsync(toDelete);
                if (toUpsert.Any()) await _db.BulkInsertOrUpdateAsync(toUpsert);
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        private async Task ProcessPricesBulk(string json)
        {
            var dtos = JsonSerializer.Deserialize<List<PriceDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dtos == null || !dtos.Any()) return;

            var toDelete = new List<Price>();
            var toUpsert = new List<Price>();

            foreach (var dto in dtos)
            {
                if (string.IsNullOrEmpty(dto.PriceTypeRef) || string.IsNullOrEmpty(dto.ProductRef)) continue;
                var entity = new Price { PriceTypeRef = dto.PriceTypeRef, ProductRef = dto.ProductRef, PriceValue = dto.PriceValue, Currency = dto.Currency };
                if (dto.IsPhysicallyDeleted == true || dto.PriceValue == 0) toDelete.Add(entity); else toUpsert.Add(entity);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                if (toDelete.Any()) await _db.BulkDeleteAsync(toDelete);
                if (toUpsert.Any()) await _db.BulkInsertOrUpdateAsync(toUpsert);
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
        }
    }
}