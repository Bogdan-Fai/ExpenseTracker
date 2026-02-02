using ExpenseTracker.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Xml.Serialization;

namespace ExpenseTracker.Services
{
    public class ReportService
    {
        private readonly string _connectionString;

        public ReportService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task GenerateMonthlyReportAsync(int year, int month)
        {
            var summary = await GetMonthlySummaryAdoNetAsync(year, month);

            if (!summary.Any())
            {
                Console.WriteLine($"Нет данных для отчета за {year:0000}-{month:00}");
                return;
            }

            // JSON отчет
            await GenerateJsonReportAsync(summary, $"{year:0000}-{month:00}-summary.json");

            // XML отчет
            await GenerateXmlReportAsync(summary, $"{year:0000}-{month:00}-summary.xml");

            Console.WriteLine($"Отчет сгенерирован за {year:0000}-{month:00}");
            Console.WriteLine($"Файлы: {year:0000}-{month:00}-summary.json и {year:0000}-{month:00}-summary.xml");

            // Вывод в консоль
            await PrintCategorySummaryToConsoleAsync(year, month);
        }

        private async Task<List<CategorySummary>> GetMonthlySummaryAdoNetAsync(int year, int month)
        {
            var summaries = new List<CategorySummary>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Для SQLite используем формат даты YYYY-MM-DD
            var startDate = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
            var endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");

            var sql = @"
                SELECT Category, SUM(Amount) as TotalAmount, COUNT(*) as TransactionCount
                FROM Transactions 
                WHERE Date >= @StartDate AND Date <= @EndDate
                GROUP BY Category
                ORDER BY TotalAmount DESC";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@StartDate", startDate);
            command.Parameters.AddWithValue("@EndDate", endDate);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summaries.Add(new CategorySummary
                {
                    Category = reader.GetString(0),
                    TotalAmount = reader.GetDecimal(1),
                    TransactionCount = reader.GetInt32(2)
                });
            }

            return summaries;
        }

        public async Task<string> GetCategorySummaryJsonAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var summaries = new List<CategorySummary>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT Category, SUM(Amount) as TotalAmount, COUNT(*) as TransactionCount
                FROM Transactions 
                WHERE (@StartDate IS NULL OR Date >= @StartDate)
                  AND (@EndDate IS NULL OR Date <= @EndDate)
                GROUP BY Category
                ORDER BY TotalAmount DESC";

            using var command = new SqliteCommand(sql, connection);

            command.Parameters.AddWithValue("@StartDate", startDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@EndDate", endDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summaries.Add(new CategorySummary
                {
                    Category = reader.GetString(0),
                    TotalAmount = reader.GetDecimal(1),
                    TransactionCount = reader.GetInt32(2)
                });
            }

            return JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true });
        }

        private async Task GenerateJsonReportAsync(List<CategorySummary> summary, string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(summary, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        private async Task GenerateXmlReportAsync(List<CategorySummary> summary, string filePath)
        {
            await using var stream = new FileStream(filePath, FileMode.Create);
            var serializer = new XmlSerializer(typeof(List<CategorySummary>));
            serializer.Serialize(stream, summary);
        }

        private async Task PrintCategorySummaryToConsoleAsync(int year, int month)
        {
            var summaries = await GetMonthlySummaryAdoNetAsync(year, month);

            Console.WriteLine($"\nАгрегированный отчет за {year:0000}-{month:00}:");
            Console.WriteLine("=========================================");
            Console.WriteLine("Категория           | Сумма      | Кол-во");
            Console.WriteLine("-----------------------------------------");

            decimal totalAmount = 0;
            int totalCount = 0;

            foreach (var summary in summaries)
            {
                Console.WriteLine($"{summary.Category,-20} | {summary.TotalAmount,10:C} | {summary.TransactionCount,6}");
                totalAmount += summary.TotalAmount;
                totalCount += summary.TransactionCount;
            }

            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"{"Итого",-20} | {totalAmount,10:C} | {totalCount,6}");
            Console.WriteLine("=========================================\n");
        }
    }
}