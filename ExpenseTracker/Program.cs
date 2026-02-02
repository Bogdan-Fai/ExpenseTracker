using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // Конфигурация
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                // Получаем строку подключения или используем SQLite по умолчанию
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Data Source=expensetracker.db";
                    Console.WriteLine("Используется SQLite база данных: expensetracker.db");
                }
                else
                {
                    // Проверяем, что строка подключения для SQLite
                    if (connectionString.Contains("Server=") || connectionString.Contains("server="))
                    {
                        Console.WriteLine("Обнаружена строка подключения SQL Server. Переключаемся на SQLite...");
                        connectionString = "Data Source=expensetracker.db";
                    }
                    Console.WriteLine($"Используется строка подключения: {connectionString}");
                }

                // Сервисы
                var services = new ServiceCollection();

                // Database context - используем SQLite
                services.AddDbContext<TransactionsContext>(options =>
                    options.UseSqlite(connectionString));

                // Services - передаем connectionString в конструкторы
                services.AddScoped<FileImportService>(provider => new FileImportService(connectionString));
                services.AddScoped<ReportService>(provider => new ReportService(connectionString));
                services.AddScoped<QueryService>(provider =>
                {
                    var context = provider.GetRequiredService<TransactionsContext>();
                    return new QueryService(context, connectionString);
                });
                services.AddSingleton<ExpenseTrackerApp>();

                var serviceProvider = services.BuildServiceProvider();

                // Инициализация БД
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TransactionsContext>();

                // Создаем базу данных если не существует
                await InitializeDatabase(context);
                Console.WriteLine("База данных SQLite инициализирована");

                // Запуск приложения
                var app = scope.ServiceProvider.GetRequiredService<ExpenseTrackerApp>();

                if (args.Length > 0)
                {
                    // Режим командной строки
                    await app.RunAsync(args);
                }
                else
                {
                    // Интерактивный режим с меню
                    await app.RunInteractiveAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка приложения: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task InitializeDatabase(TransactionsContext context)
        {
            try
            {
                // Создаем базу данных и применяем миграции
                await context.Database.EnsureCreatedAsync();

                // Проверяем, есть ли данные в базе
                var transactionCount = await context.Transactions.CountAsync();
                Console.WriteLine($"В базе данных найдено записей: {transactionCount}");

                if (transactionCount == 0)
                {
                    Console.WriteLine("База данных пуста. Будут добавлены тестовые данные...");

                    // Добавляем тестовые данные вручную, так как HasData может не работать с SQLite
                    context.Transactions.AddRange(
                        new Transaction { Date = new DateTime(2024, 1, 15), Category = "Еда", Amount = 1500.50m, Note = "Продукты на неделю" },
                        new Transaction { Date = new DateTime(2024, 1, 20), Category = "Транспорт", Amount = 500.00m, Note = "Бензин" },
                        new Transaction { Date = new DateTime(2024, 2, 5), Category = "Развлечения", Amount = 1200.00m, Note = "Кино" },
                        new Transaction { Date = new DateTime(2024, 2, 10), Category = "Еда", Amount = 800.25m, Note = "Ресторан" },
                        new Transaction { Date = new DateTime(2024, 2, 15), Category = "Коммунальные", Amount = 3500.75m, Note = "Квартплата" }
                    );

                    await context.SaveChangesAsync();
                    Console.WriteLine("Тестовые данные добавлены успешно.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при инициализации базы данных: {ex.Message}");
                throw;
            }
        }
    }

    public class ExpenseTrackerApp
    {
        private readonly FileImportService _importService;
        private readonly ReportService _reportService;
        private readonly QueryService _queryService;

        public ExpenseTrackerApp(
            FileImportService importService,
            ReportService reportService,
            QueryService queryService)
        {
            _importService = importService;
            _reportService = reportService;
            _queryService = queryService;
        }

        // Интерактивный режим с меню
        public async Task RunInteractiveAsync()
        {
            while (true)
            {
                Console.Clear();
                DisplayMenu();

                var choice = GetMenuChoice();

                switch (choice)
                {
                    case 1:
                        await HandleImportInteractive();
                        break;
                    case 2:
                        await HandleReportInteractive();
                        break;
                    case 3:
                        await HandleQueryInteractive();
                        break;
                    case 4:
                        await HandleExportInteractive();
                        break;
                    case 5:
                        await HandleAggregateInteractive();
                        break;
                    case 6:
                        await ExecuteTask1Interactive();
                        break;
                    case 7:
                        await ExecuteTask2Interactive();
                        break;
                    case 8:
                        await TestDatabase();
                        break;
                    case 0:
                        Console.WriteLine("Выход из приложения...");
                        return;
                    default:
                        Console.WriteLine("Неверный выбор. Попробуйте снова.");
                        break;
                }

                Console.WriteLine("\nНажмите любую клавишу для продолжения...");
                Console.ReadKey();
            }
        }

        private void DisplayMenu()
        {
            Console.WriteLine("----------------------------");
            Console.WriteLine("     EXPENSE TRACKER");
            Console.WriteLine("----------------------------");
            Console.WriteLine("1. Импорт транзакций из файла");
            Console.WriteLine("2. Генерация месячного отчета");
            Console.WriteLine("3. Поиск транзакций с фильтрами");
            Console.WriteLine("4. Экспорт всех транзакций");
            Console.WriteLine("5. Агрегированные данные по категориям");
            Console.WriteLine("6. ВЫПОЛНИТЬ ЗАДАНИЕ 1 (Импорт + отчеты)");
            Console.WriteLine("7. ВЫПОЛНИТЬ ЗАДАНИЕ 2 (EF Core + агрегации)");
            Console.WriteLine("8. Тест подключения к базе данных");
            Console.WriteLine("0. Выход");
            Console.WriteLine("----------------------------");
            Console.Write("Выберите опцию (0-8): ");
        }

        private int GetMenuChoice()
        {
            if (int.TryParse(Console.ReadLine(), out int choice))
            {
                return choice;
            }
            return -1;
        }

        private List<string> FindAvailableFiles()
        {
            var availableFiles = new List<string>();
            var rootDirectory = Directory.GetCurrentDirectory();

            try
            {
                // Ищем файлы в корневой папке и подпапках
                var searchPatterns = new[] { "transactions*.txt", "transactions*.jsonl", "transactions*.json", "*.txt", "*.jsonl", "*.json" };

                foreach (var pattern in searchPatterns)
                {
                    try
                    {
                        var files = Directory.GetFiles(rootDirectory, pattern, SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            if (!availableFiles.Contains(file))
                                availableFiles.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при поиске файлов по шаблону {pattern}: {ex.Message}");
                    }
                }

                // Убираем дубликаты
                availableFiles = availableFiles.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске файлов: {ex.Message}");
            }

            // Получаем относительные пути для удобного отображения
            return availableFiles.Select(f =>
            {
                var relativePath = Path.GetRelativePath(rootDirectory, f);
                return relativePath == f ? Path.GetFileName(f) : relativePath;
            }).ToList();
        }

        private async Task ImportFile(string fileName)
        {
            try
            {
                var rootDirectory = Directory.GetCurrentDirectory();
                var filePath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(rootDirectory, fileName);

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Файл не найден: {filePath}");
                    return;
                }

                var fileExtension = Path.GetExtension(fileName).ToLower();
                Console.WriteLine($"\nИмпорт из файла: {fileName}");

                if (fileExtension == ".jsonl")
                {
                    var result = await _importService.ImportFromJsonLFileAsync(filePath);
                    PrintImportResult(result, "JSONL");
                }
                else if (fileExtension == ".json")
                {
                    var result = await _importService.ImportFromJsonFileAsync(filePath);
                    PrintImportResult(result, "JSON");
                }
                else
                {
                    var result = await _importService.ImportFromTextFileAsync(filePath);
                    PrintImportResult(result, "текстовый");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при импорте файла: {ex.Message}");
            }
        }

        private async Task HandleImportInteractive()
        {
            Console.Clear();
            Console.WriteLine("=== ИМПОРТ ТРАНЗАКЦИЙ ИЗ ФАЙЛА ===");

            // Автоматический поиск файлов в корневой папке
            var availableFiles = FindAvailableFiles();

            if (!availableFiles.Any())
            {
                Console.WriteLine("В папке приложения не найдено файлов для импорта.");
                Console.WriteLine("Разместите файлы transactions.txt или transactions.jsonl в папке с программой.");
                return;
            }

            Console.WriteLine("Найдены файлы для импорта:");
            for (int i = 0; i < availableFiles.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {availableFiles[i]}");
            }

            Console.WriteLine($"{availableFiles.Count + 1}. Ввести путь вручную");

            Console.Write($"\nВыберите файл (1-{availableFiles.Count + 1}): ");
            if (int.TryParse(Console.ReadLine(), out int fileChoice))
            {
                if (fileChoice >= 1 && fileChoice <= availableFiles.Count)
                {
                    var selectedFile = availableFiles[fileChoice - 1];
                    await ImportFile(selectedFile);
                }
                else if (fileChoice == availableFiles.Count + 1)
                {
                    Console.Write("Введите путь к файлу: ");
                    var manualPath = Console.ReadLine();
                    if (!string.IsNullOrEmpty(manualPath))
                    {
                        await ImportFile(manualPath);
                    }
                }
                else
                {
                    Console.WriteLine("Неверный выбор файла.");
                }
            }
            else
            {
                Console.WriteLine("Неверный выбор файла.");
            }
        }

        private async Task HandleReportInteractive()
        {
            Console.Clear();
            Console.WriteLine("=== ГЕНЕРАЦИЯ МЕСЯЧНОГО ОТЧЕТА ===");

            // Автоматическое определение текущего года и месяца
            var currentDate = DateTime.Now;
            var lastMonth = currentDate.AddMonths(-1);

            Console.WriteLine($"Текущий период: {lastMonth:yyyy-MM} (автоматически)");
            Console.Write("Использовать текущий период? (y/n): ");
            var useCurrent = Console.ReadLine()?.ToLower() == "y";

            int year, month;

            if (useCurrent)
            {
                year = lastMonth.Year;
                month = lastMonth.Month;
            }
            else
            {
                Console.Write("Введите год: ");
                if (!int.TryParse(Console.ReadLine(), out year))
                {
                    Console.WriteLine("Неверный формат года.");
                    return;
                }

                Console.Write("Введите месяц: ");
                if (!int.TryParse(Console.ReadLine(), out month))
                {
                    Console.WriteLine("Неверный формат месяца.");
                    return;
                }
            }

            await _reportService.GenerateMonthlyReportAsync(year, month);
        }

        private async Task HandleQueryInteractive()
        {
            Console.Clear();
            Console.WriteLine("=== ПОИСК ТРАНЗАКЦИЙ С ФИЛЬТРАМИ ===");

            DateTime? startDate = null, endDate = null;
            string? category = null;
            string sortBy = "Date";
            bool descending = true;
            int page = 1, pageSize = 50;

            // Автоматическая установка периода за последний месяц
            var lastMonth = DateTime.Now.AddMonths(-1);
            var defaultStart = new DateTime(lastMonth.Year, lastMonth.Month, 1);
            var defaultEnd = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));

            Console.WriteLine($"Период по умолчанию: {defaultStart:yyyy-MM-dd} - {defaultEnd:yyyy-MM-dd}");
            Console.Write("Использовать период по умолчанию? (y/n): ");
            var useDefaultPeriod = Console.ReadLine()?.ToLower() == "y";

            if (useDefaultPeriod)
            {
                startDate = defaultStart;
                endDate = defaultEnd;
            }
            else
            {
                Console.Write("Начальная дата (YYYY-MM-DD, Enter для пропуска): ");
                var startInput = Console.ReadLine();
                if (!string.IsNullOrEmpty(startInput) && DateTime.TryParse(startInput, out DateTime start))
                    startDate = start;

                Console.Write("Конечная дата (YYYY-MM-DD, Enter для пропуска): ");
                var endInput = Console.ReadLine();
                if (!string.IsNullOrEmpty(endInput) && DateTime.TryParse(endInput, out DateTime end))
                    endDate = end;
            }

            Console.Write("Категория (Enter для пропуска): ");
            var categoryInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(categoryInput))
                category = categoryInput;

            Console.Write("Сортировка (Date/Amount/Category, по умолчанию Date): ");
            var sortInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(sortInput))
                sortBy = sortInput;

            Console.Write("Порядок сортировки (1 - по убыванию, 2 - по возрастанию): ");
            var orderInput = Console.ReadLine();
            if (orderInput == "2")
                descending = false;

            Console.Write("Номер страницы (по умолчанию 1): ");
            if (int.TryParse(Console.ReadLine(), out int pageInput))
                page = pageInput;

            Console.Write("Размер страницы (по умолчанию 50): ");
            if (int.TryParse(Console.ReadLine(), out int sizeInput))
                pageSize = sizeInput;

            var result = await _queryService.GetTransactionsAsync(
                startDate, endDate, category, sortBy, descending, page, pageSize);

            Console.WriteLine($"\nНайдено записей: {result.totalCount} (показано: {result.data.Count})");
            Console.WriteLine("Дата       | Категория       | Сумма       | Примечание");
            Console.WriteLine("---------------------------------------------------------");

            foreach (var item in result.data)
            {
                Console.WriteLine($"{item.Date:yyyy-MM-dd} | {item.Category,-15} | {item.Amount,10:C} | {item.Note}");
            }
        }

        private async Task HandleExportInteractive()
        {
            Console.Clear();
            Console.WriteLine("=== ЭКСПОРТ ВСЕХ ТРАНЗАКЦИЙ ===");

            var result = await _queryService.GetTransactionsAsync(pageSize: int.MaxValue);
            await _queryService.ExportTransactionsAsync(result.data);
        }

        private async Task HandleAggregateInteractive()
        {
            Console.Clear();
            Console.WriteLine("=== АГРЕГИРОВАННЫЕ ДАННЫЕ ПО КАТЕГОРИЯМ ===");

            DateTime? startDate = null, endDate = null;

            // Автоматическая установка периода за последние 3 месяца
            var threeMonthsAgo = DateTime.Now.AddMonths(-3);
            var defaultStart = new DateTime(threeMonthsAgo.Year, threeMonthsAgo.Month, 1);

            Console.WriteLine($"Период по умолчанию: с {defaultStart:yyyy-MM-dd} по сегодня");
            Console.Write("Использовать период по умолчанию? (y/n): ");
            var useDefaultPeriod = Console.ReadLine()?.ToLower() == "y";

            if (useDefaultPeriod)
            {
                startDate = defaultStart;
            }
            else
            {
                Console.Write("Начальная дата (YYYY-MM-DD, Enter для пропуска): ");
                var startInput = Console.ReadLine();
                if (!string.IsNullOrEmpty(startInput) && DateTime.TryParse(startInput, out DateTime start))
                    startDate = start;

                Console.Write("Конечная дата (YYYY-MM-DD, Enter для пропуска): ");
                var endInput = Console.ReadLine();
                if (!string.IsNullOrEmpty(endInput) && DateTime.TryParse(endInput, out DateTime end))
                    endDate = end;
            }

            var jsonResult = await _reportService.GetCategorySummaryJsonAsync(startDate, endDate);
            Console.WriteLine("\nАгрегированные данные (JSON):");
            Console.WriteLine(jsonResult);

            var fileName = "aggregate_summary.json";
            await File.WriteAllTextAsync(fileName, jsonResult);
            Console.WriteLine($"\nДанные также сохранены в файл: {fileName}");
        }

        private async Task ExecuteTask1Interactive()
        {
            Console.Clear();
            Console.WriteLine("=== ВЫПОЛНЕНИЕ ЗАДАНИЯ 1 ===");

            // Автоматический поиск файлов
            var availableFiles = FindAvailableFiles();
            var txtFiles = availableFiles.Where(f => f.EndsWith(".txt")).ToList();

            if (!txtFiles.Any())
            {
                Console.WriteLine("В папке приложения не найдено текстовых файлов для задания 1.");
                Console.WriteLine("Разместите файл transactions.txt в папке с программой.");
                return;
            }

            string selectedFile;
            if (txtFiles.Count == 1)
            {
                selectedFile = txtFiles[0];
                Console.WriteLine($"Автоматически выбран файл: {selectedFile}");
            }
            else
            {
                Console.WriteLine("Найдены текстовые файлы:");
                for (int i = 0; i < txtFiles.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {txtFiles[i]}");
                }

                Console.Write($"\nВыберите файл (1-{txtFiles.Count}): ");
                if (int.TryParse(Console.ReadLine(), out int fileChoice) && fileChoice >= 1 && fileChoice <= txtFiles.Count)
                {
                    selectedFile = txtFiles[fileChoice - 1];
                }
                else
                {
                    Console.WriteLine("Неверный выбор файла.");
                    return;
                }
            }

            await ExecuteTask1(new[] { "task1", selectedFile });
        }

        private async Task ExecuteTask2Interactive()
        {
            Console.Clear();
            Console.WriteLine("=== ВЫПОЛНЕНИЕ ЗАДАНИЯ 2 ===");
            await ExecuteTask2(new string[0]);
        }

        // Оригинальные методы для работы через командную строки
        public async Task RunAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            try
            {
                switch (args[0].ToLower())
                {
                    case "import":
                        await HandleImport(args);
                        break;
                    case "report":
                        await HandleReport(args);
                        break;
                    case "query":
                        await HandleQuery(args);
                        break;
                    case "export":
                        await HandleExport();
                        break;
                    case "aggregate":
                        await HandleAggregate(args);
                        break;
                    case "test":
                        await TestDatabase();
                        break;
                    case "task1":
                        await ExecuteTask1(args);
                        break;
                    case "task2":
                        await ExecuteTask2(args);
                        break;
                    default:
                        ShowHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        // ЗАДАНИЕ 1: Импорт через ADO.NET и генерация отчета
        private async Task ExecuteTask1(string[] args)
        {
            Console.WriteLine("=== ВЫПОЛНЕНИЕ ЗАДАНИЯ 1 ===");

            string filePath;
            if (args.Length < 2)
            {
                // Автоматический поиск файла
                var availableFiles = FindAvailableFiles();
                var txtFiles = availableFiles.Where(f => f.EndsWith(".txt")).ToList();

                if (!txtFiles.Any())
                {
                    Console.WriteLine("Ошибка: В папке приложения не найдено файлов transactions.txt");
                    return;
                }

                filePath = Path.Combine(Directory.GetCurrentDirectory(), txtFiles[0]);
                Console.WriteLine($"Автоматически выбран файл: {txtFiles[0]}");
            }
            else
            {
                filePath = Path.Combine(Directory.GetCurrentDirectory(), args[1]);
            }

            // 1. Прочитать файл построчно и распарсить поля
            Console.WriteLine($"\n1. Чтение и парсинг файла: {Path.GetFileName(filePath)}");
            var result = await _importService.ImportFromTextFileAsync(filePath);

            // Логирование результатов
            Console.WriteLine($"\nРезультаты импорта:");
            Console.WriteLine($"- Успешно импортировано: {result.imported} записей");
            Console.WriteLine($"- Обнаружено ошибок: {result.errors}");

            if (result.errorMessages.Any())
            {
                Console.WriteLine("\nОшибки при импорте:");
                foreach (var error in result.errorMessages.Take(10))
                {
                    Console.WriteLine($"  {error}");
                }
                if (result.errorMessages.Count > 10)
                {
                    Console.WriteLine($"  ... и еще {result.errorMessages.Count - 10} ошибок");
                }
            }

            // 3. Генерация месячного отчета
            if (result.imported > 0)
            {
                Console.WriteLine($"\n2. Генерация месячных отчетов...");
                await GenerateMonthlyReportsForImportedData();
            }
            else
            {
                Console.WriteLine("Нет данных для генерации отчетов");
            }

            Console.WriteLine("\n=== ЗАДАНИЕ 1 ВЫПОЛНЕНО ===");
        }

        private async Task GenerateMonthlyReportsForImportedData()
        {
            try
            {
                // Получаем список месяцев из импортированных данных
                var allTransactions = await _queryService.GetTransactionsAsync(pageSize: int.MaxValue);
                var months = allTransactions.data
                    .Select(t => (t.Date.Year, t.Date.Month))
                    .Distinct()
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToList();

                foreach (var (year, month) in months)
                {
                    Console.WriteLine($"Генерация отчета за {year:0000}-{month:00}...");
                    await _reportService.GenerateMonthlyReportAsync(year, month);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при генерации отчетов: {ex.Message}");
            }
        }

        // ЗАДАНИЕ 2: EF Core + Агрегатные запросы + Экспорт
        private async Task ExecuteTask2(string[] args)
        {
            Console.WriteLine("=== ВЫПОЛНЕНИЕ ЗАДАНИЕ 2 ===");

            // 1. Демонстрация EF Core модели и миграций
            Console.WriteLine("\n1. Проверка EF Core модели и контекста...");
            await TestDatabase();

            // 2. Демонстрация API для сортировки, пагинации, фильтрации
            Console.WriteLine("\n2. Демонстрация запросов с фильтрацией и сортировкой...");
            await DemonstrateQueryFeatures();

            // 3. Быстрый агрегатный запрос через ADO.NET
            Console.WriteLine("\n3. Быстрый агрегатный запрос через ADO.NET...");
            await DemonstrateAdoNetAggregate();

            // 4. Экспорт выборки
            Console.WriteLine("\n4. Экспорт данных...");
            await HandleExport();

            Console.WriteLine("\n=== ЗАДАНИЕ 2 ВЫПОЛНЕНО ===");
        }

        private async Task DemonstrateQueryFeatures()
        {
            // Демонстрация различных сценариев использования EF Core
            Console.WriteLine("\nа) Все транзакции (с пагинацией):");
            await ExecuteAndDisplayQuery(() => _queryService.GetTransactionsAsync(page: 1, pageSize: 5));

            Console.WriteLine("\nб) Сортировка по сумме (по убыванию):");
            await ExecuteAndDisplayQuery(() => _queryService.GetTransactionsAsync(sortBy: "Amount", descending: true, pageSize: 5));

            Console.WriteLine("\nв) Сортировка по дате (по возрастанию):");
            await ExecuteAndDisplayQuery(() => _queryService.GetTransactionsAsync(sortBy: "Date", descending: false, pageSize: 5));

            Console.WriteLine("\nг) Фильтрация по категории 'Еда':");
            await ExecuteAndDisplayQuery(() => _queryService.GetTransactionsAsync(category: "Еда", pageSize: 5));
        }

        private async Task ExecuteAndDisplayQuery(Func<Task<(List<Transaction> data, int totalCount)>> queryAction)
        {
            try
            {
                var result = await queryAction();
                Console.WriteLine($"Найдено записей: {result.totalCount} (показано: {result.data.Count})");

                if (result.data.Any())
                {
                    Console.WriteLine("Дата       | Категория       | Сумма       | Примечание");
                    Console.WriteLine("---------------------------------------------------------");
                    foreach (var item in result.data)
                    {
                        Console.WriteLine($"{item.Date:yyyy-MM-dd} | {item.Category,-15} | {item.Amount,10:C} | {item.Note}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private async Task DemonstrateAdoNetAggregate()
        {
            try
            {
                // Агрегация за последние 3 месяца
                var startDate = DateTime.Now.AddMonths(-3);
                var jsonResult = await _reportService.GetCategorySummaryJsonAsync(startDate: startDate);

                Console.WriteLine("Агрегированные данные по категориям (последние 3 месяца):");
                Console.WriteLine(jsonResult);

                // Сохранение в файл
                var fileName = $"aggregate_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                await File.WriteAllTextAsync(fileName, jsonResult);
                Console.WriteLine($"\nАгрегированные данные сохранены в файл: {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при выполнении агрегатного запроса: {ex.Message}");
            }
        }

        private void PrintImportResult((int imported, int errors, List<string> errorMessages) result, string fileType)
        {
            Console.WriteLine($"\nРезультат импорта из {fileType} файла:");
            Console.WriteLine($"Импортировано записей: {result.imported}");
            Console.WriteLine($"Ошибок: {result.errors}");

            if (result.errorMessages.Any())
            {
                Console.WriteLine("\nСообщения об ошибках:");
                foreach (var error in result.errorMessages.Take(10))
                {
                    Console.WriteLine($"  - {error}");
                }
                if (result.errorMessages.Count > 10)
                {
                    Console.WriteLine($"  ... и еще {result.errorMessages.Count - 10} ошибок");
                }
            }
        }

        private async Task HandleImport(string[] args)
        {
            string filePath;
            if (args.Length < 2)
            {
                // Автоматический поиск файла
                var availableFiles = FindAvailableFiles();
                if (!availableFiles.Any())
                {
                    Console.WriteLine("В папке приложения не найдено файлов для импорта.");
                    return;
                }
                filePath = Path.Combine(Directory.GetCurrentDirectory(), availableFiles[0]);
                Console.WriteLine($"Автоматически выбран файл: {availableFiles[0]}");
            }
            else
            {
                filePath = Path.Combine(Directory.GetCurrentDirectory(), args[1]);
            }

            var fileExtension = Path.GetExtension(filePath).ToLower();

            if (fileExtension == ".jsonl")
            {
                var result = await _importService.ImportFromJsonLFileAsync(filePath);
                PrintImportResult(result, "JSONL");
            }
            else
            {
                var result = await _importService.ImportFromTextFileAsync(filePath);
                PrintImportResult(result, "текстовый");
            }
        }

        private async Task HandleReport(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Укажите год и месяц: report 2024 1");
                return;
            }

            if (int.TryParse(args[1], out int year) && int.TryParse(args[2], out int month))
            {
                await _reportService.GenerateMonthlyReportAsync(year, month);
            }
            else
            {
                Console.WriteLine("Неверный формат года или месяца");
            }
        }

        private async Task HandleQuery(string[] args)
        {
            DateTime? startDate = null, endDate = null;
            string? category = null;
            string sortBy = "Date";
            bool descending = true;
            int page = 1, pageSize = 50;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--start":
                        if (i + 1 < args.Length && DateTime.TryParse(args[++i], out DateTime start))
                            startDate = start;
                        break;
                    case "--end":
                        if (i + 1 < args.Length && DateTime.TryParse(args[++i], out DateTime end))
                            endDate = end;
                        break;
                    case "--category":
                        if (i + 1 < args.Length)
                            category = args[++i];
                        break;
                    case "--sort":
                        if (i + 1 < args.Length)
                            sortBy = args[++i];
                        break;
                    case "--page":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int p))
                            page = p;
                        break;
                    case "--size":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int ps))
                            pageSize = ps;
                        break;
                    case "--asc":
                        descending = false;
                        break;
                }
            }

            var result = await _queryService.GetTransactionsAsync(
                startDate, endDate, category, sortBy, descending, page, pageSize);

            Console.WriteLine($"\nНайдено записей: {result.totalCount} (показано: {result.data.Count})");
            Console.WriteLine("Дата       | Категория       | Сумма       | Примечание");
            Console.WriteLine("---------------------------------------------------------");

            foreach (var item in result.data)
            {
                Console.WriteLine($"{item.Date:yyyy-MM-dd} | {item.Category,-15} | {item.Amount,10:C} | {item.Note}");
            }
        }

        private async Task HandleExport()
        {
            var result = await _queryService.GetTransactionsAsync(pageSize: int.MaxValue);
            await _queryService.ExportTransactionsAsync(result.data);
        }

        private async Task HandleAggregate(string[] args)
        {
            DateTime? startDate = null, endDate = null;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--start":
                        if (i + 1 < args.Length && DateTime.TryParse(args[++i], out DateTime start))
                            startDate = start;
                        break;
                    case "--end":
                        if (i + 1 < args.Length && DateTime.TryParse(args[++i], out DateTime end))
                            endDate = end;
                        break;
                }
            }

            var jsonResult = await _reportService.GetCategorySummaryJsonAsync(startDate, endDate);
            Console.WriteLine("\nАгрегированные данные (JSON):");
            Console.WriteLine(jsonResult);

            var fileName = "aggregate_summary.json";
            await File.WriteAllTextAsync(fileName, jsonResult);
            Console.WriteLine($"\nДанные также сохранены в файл: {fileName}");
        }

        private async Task TestDatabase()
        {
            try
            {
                var result = await _queryService.GetTransactionsAsync(pageSize: 5);
                Console.WriteLine($"Тест базы данных успешен. Записей в базе: {result.totalCount}");

                if (result.data.Any())
                {
                    Console.WriteLine("Примеры записей:");
                    foreach (var item in result.data)
                    {
                        Console.WriteLine($"{item.Date:yyyy-MM-dd} | {item.Category} | {item.Amount:C} | {item.Note}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Тест базы данных не пройден: {ex.Message}");
            }
        }

        private void ShowHelp()
        {
            Console.WriteLine(@"
ExpenseTracker - Управление финансовыми транзакциями

Команды:
  import <file>          - Импорт транзакций из файла (txt или jsonl)
  report <year> <month>  - Генерация месячного отчета
  query [options]        - Запрос транзакций с фильтрами
  export                 - Экспорт всех транзакций
  aggregate [options]    - Агрегированные данные по категориям
  test                   - Тест подключения к базе данных

  task1 <file>           - ВЫПОЛНИТЬ ЗАДАНИЕ 1: Импорт + отчеты
  task2                  - ВЫПОЛНИТЬ ЗАДАНИЕ 2: EF Core + агрегации + экспорт

Опции запроса:
  --start YYYY-MM-DD     - Начальная дата
  --end YYYY-MM-DD       - Конечная дата  
  --category NAME        - Фильтр по категории
  --sort FIELD           - Поле сортировки (Date, Amount, Category)
  --page NUMBER          - Номер страницы (по умолчанию: 1)
  --size NUMBER          - Размер страницы (по умолчанию: 50)
  --asc                  - Сортировка по возрастанию

Примеры:
  task1 transactions.txt             - Выполнить задание 1
  task2                              - Выполнить задание 2
  import transactions.txt            - Импорт из текстового файла
  import transactions_sample.jsonl   - Импорт из JSONL файла
  report 2024 1                      - Отчет за январь 2024
  query --start 2024-01-01 --category Еда --sort Amount --desc
  aggregate --start 2024-01-01 --end 2024-03-31

Запуск без параметров - интерактивный режим с меню.
            ");
        }
    }
}