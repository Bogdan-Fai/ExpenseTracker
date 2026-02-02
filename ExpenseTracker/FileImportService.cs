using ExpenseTracker.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;

namespace ExpenseTracker.Services
{
    public class FileImportService
    {
        private readonly string _connectionString;

        public FileImportService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }

        public async Task<(int imported, int errors, List<string> errorMessages)> ImportFromTextFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));

            var imported = 0;
            var errors = 0;
            var errorMessages = new List<string>();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл {filePath} не найден");
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            var transactions = new List<Transaction>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var transaction = ParseTransactionLine(line, i + 1);
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                    else
                    {
                        errors++;
                        errorMessages.Add($"Строка {i + 1}: Неверный формат данных");
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    errorMessages.Add($"Строка {i + 1}: {ex.Message}");
                }
            }

            if (transactions.Any())
            {
                imported = await BulkInsertTransactionsAdoNetAsync(transactions);
            }

            return (imported, errors, errorMessages);
        }

        public async Task<(int imported, int errors, List<string> errorMessages)> ImportFromJsonLFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));

            var imported = 0;
            var errors = 0;
            var errorMessages = new List<string>();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл {filePath} не найден");
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            var transactions = new List<Transaction>();

            // Настройки для десериализации JSON
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var transaction = JsonSerializer.Deserialize<Transaction>(line, jsonOptions);
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                    else
                    {
                        errors++;
                        errorMessages.Add($"Строка {i + 1}: Не удалось десериализовать JSON");
                    }
                }
                catch (JsonException jsonEx)
                {
                    errors++;
                    errorMessages.Add($"Строка {i + 1}: Ошибка JSON - {jsonEx.Message}");
                }
                catch (Exception ex)
                {
                    errors++;
                    errorMessages.Add($"Строка {i + 1}: {ex.Message}");
                }
            }

            if (transactions.Any())
            {
                imported = await BulkInsertTransactionsAdoNetAsync(transactions);
            }

            return (imported, errors, errorMessages);
        }

        public async Task<(int imported, int errors, List<string> errorMessages)> ImportFromJsonFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));

            var imported = 0;
            var errors = 0;
            var errorMessages = new List<string>();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл {filePath} не найден");
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);

                // Настройки для десериализации JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };

                var transactions = JsonSerializer.Deserialize<List<Transaction>>(jsonContent, jsonOptions);

                if (transactions != null && transactions.Any())
                {
                    imported = await BulkInsertTransactionsAdoNetAsync(transactions);
                }
                else
                {
                    errors++;
                    errorMessages.Add("Файл не содержит данных или имеет неверный формат");
                }
            }
            catch (JsonException jsonEx)
            {
                errors++;
                errorMessages.Add($"Ошибка JSON: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"Ошибка: {ex.Message}");
            }

            return (imported, errors, errorMessages);
        }

        private Transaction? ParseTransactionLine(string line, int lineNumber)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                throw new FormatException($"Недостаточно данных. Ожидается: Дата|Категория|Сумма|[Примечание]");
            }

            return new Transaction
            {
                Date = DateTime.ParseExact(parts[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Category = parts[1].Trim(),
                Amount = decimal.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
                Note = parts.Length > 3 ? parts[3].Trim() : string.Empty
            };
        }

        private async Task<int> BulkInsertTransactionsAdoNetAsync(List<Transaction> transactions)
        {
            if (!transactions.Any()) return 0;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var inserted = 0;

            foreach (var trans in transactions)
            {
                try
                {
                    using var command = new SqliteCommand(
                        @"INSERT INTO Transactions (Date, Category, Amount, Note) 
                          VALUES (@Date, @Category, @Amount, @Note)",
                        connection);

                    // SQLite хранит даты как строки в формате YYYY-MM-DD
                    command.Parameters.AddWithValue("@Date", trans.Date.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("@Category", trans.Category);
                    command.Parameters.AddWithValue("@Amount", trans.Amount);
                    command.Parameters.AddWithValue("@Note", string.IsNullOrEmpty(trans.Note) ? (object)DBNull.Value : trans.Note);

                    inserted += await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при вставке транзакции: {ex.Message}");
                    // Продолжаем со следующей транзакцией
                }
            }

            return inserted;
        }
    }
}