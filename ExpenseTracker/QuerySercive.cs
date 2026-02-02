using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ExpenseTracker.Services
{
    public class QueryService
    {
        private readonly TransactionsContext _context;
        private readonly string _connectionString;

        public QueryService(TransactionsContext context, string connectionString)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<(List<Transaction> data, int totalCount)> GetTransactionsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? category = null,
            string sortBy = "Date",
            bool descending = true,
            int page = 1,
            int pageSize = 50)
        {
            var query = _context.Transactions.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(t => t.Category == category);

            var totalCount = await query.CountAsync();

            // Сортировка через EF Core
            query = sortBy.ToLower() switch
            {
                "amount" => descending ?
                    query.OrderByDescending(t => t.Amount) :
                    query.OrderBy(t => t.Amount),
                "category" => descending ?
                    query.OrderByDescending(t => t.Category) :
                    query.OrderBy(t => t.Category),
                _ => descending ?
                    query.OrderByDescending(t => t.Date) :
                    query.OrderBy(t => t.Date)
            };

            // Пагинация через EF Core
            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, totalCount);
        }

        public async Task ExportTransactionsAsync(List<Transaction> transactions, string baseFileName = "transactions_export")
        {
            if (transactions == null)
                throw new ArgumentNullException(nameof(transactions));

            // JSON экспорт
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(transactions, jsonOptions);
            await File.WriteAllTextAsync($"{baseFileName}.json", json);

            // XML экспорт
            await using var stream = new FileStream($"{baseFileName}.xml", FileMode.Create);
            var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(List<Transaction>));
            xmlSerializer.Serialize(stream, transactions);

            Console.WriteLine($"Данные экспортированы в {baseFileName}.json и {baseFileName}.xml");
            Console.WriteLine($"Экспортировано записей: {transactions.Count}");
        }
    }
}