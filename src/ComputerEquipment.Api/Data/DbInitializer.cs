using ComputerEquipment.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComputerEquipment.Api.Data;

/// <summary>Применяет миграции и заполняет словарь типов и демо-данные, если таблицы пустые.</summary>
public static class DbInitializer
{
    /// <summary>Ключ advisory-блокировки, под которой выполняется заполнение.</summary>
    private const long SeedLockKey = 7_100_002;
    /// <summary>Сколько раз пытаться подключиться к PostgreSQL при старте.</summary>
    private const int MaxAttempts = 10;

    /// <summary>Типы техники, которые добавляются в пустой словарь.</summary>
    internal static readonly string[] DefaultTypes =
    [
        "Системный блок",
        "Ноутбук",
        "Монитор",
        "Принтер",
        "Сетевое оборудование",
    ];

    /// <summary>Демо-записи (по 10 на тип), которые добавляются в пустую таблицу техники.</summary>
    internal static readonly (string Number, string Type, string Name, int Room)[] DefaultItems =
    [
        ("PC0001", "Системный блок", "Dell OptiPlex 7010", 101),
        ("PC0002", "Системный блок", "HP ProDesk 400 G9", 102),
        ("PC0003", "Системный блок", "Lenovo ThinkCentre M70q Gen 4", 103),
        ("PC0004", "Системный блок", "Acer Veriton X2690G", 104),
        ("PC0005", "Системный блок", "ASUS ExpertCenter D7", 118),
        ("PC0006", "Системный блок", "Aquarius Pro P30 K43", 121),
        ("PC0007", "Системный блок", "iRU Office 313", 305),
        ("PC0008", "Системный блок", "Dell Vostro 3910", 306),
        ("PC0009", "Системный блок", "HP EliteDesk 800 G9", 410),
        ("PC0010", "Системный блок", "Lenovo ThinkCentre neo 50s", 512),
        ("NB0001", "Ноутбук", "Lenovo ThinkPad T14 Gen 4", 205),
        ("NB0002", "Ноутбук", "ASUS ExpertBook B1", 207),
        ("NB0003", "Ноутбук", "HP ProBook 450 G10", 208),
        ("NB0004", "Ноутбук", "Dell Latitude 5440", 210),
        ("NB0005", "Ноутбук", "Acer TravelMate P2", 212),
        ("NB0006", "Ноутбук", "Huawei MateBook D 16", 214),
        ("NB0007", "Ноутбук", "Lenovo IdeaPad Slim 3", 301),
        ("NB0008", "Ноутбук", "MSI Modern 15", 302),
        ("NB0009", "Ноутбук", "HONOR MagicBook X 16", 615),
        ("NB0010", "Ноутбук", "Apple MacBook Air 13 M2", 700),
        ("MN0001", "Монитор", "Samsung S24C310", 101),
        ("MN0002", "Монитор", "LG 27UL500-W", 310),
        ("MN0003", "Монитор", "Dell P2422H", 102),
        ("MN0004", "Монитор", "AOC 24B2XH", 103),
        ("MN0005", "Монитор", "Philips 243V7QDSB", 104),
        ("MN0006", "Монитор", "BenQ GW2480", 205),
        ("MN0007", "Монитор", "ASUS VA24EHE", 207),
        ("MN0008", "Монитор", "Iiyama ProLite XU2493HS", 410),
        ("MN0009", "Монитор", "HP P24 G5", 512),
        ("MN0010", "Монитор", "Xiaomi Mi Desktop Monitor 27", 999),
        ("PR0001", "Принтер", "HP LaserJet Pro M404dn", 115),
        ("PR0002", "Принтер", "Canon i-SENSYS MF445dw", 220),
        ("PR0003", "Принтер", "Brother HL-L2340DWR", 120),
        ("PR0004", "Принтер", "Kyocera ECOSYS P2335dn", 230),
        ("PR0005", "Принтер", "Xerox B210", 315),
        ("PR0006", "Принтер", "Pantum P3010DW", 320),
        ("PR0007", "Принтер", "Epson L3260", 415),
        ("PR0008", "Принтер", "Ricoh SP 230DNw", 520),
        ("PR0009", "Принтер", "HP Color LaserJet Pro M255dw", 640),
        ("PR0010", "Принтер", "Canon PIXMA G3420", 777),
        ("NW0001", "Сетевое оборудование", "Cisco Catalyst 2960-X", 1),
        ("NW0002", "Сетевое оборудование", "MikroTik hAP ax3", 412),
        ("NW0003", "Сетевое оборудование", "TP-Link TL-SG1024D", 2),
        ("NW0004", "Сетевое оборудование", "Keenetic Giga", 3),
        ("NW0005", "Сетевое оборудование", "D-Link DGS-1100-24P", 100),
        ("NW0006", "Сетевое оборудование", "Ubiquiti UniFi U6 Lite", 200),
        ("NW0007", "Сетевое оборудование", "Eltex MES2324", 1),
        ("NW0008", "Сетевое оборудование", "MikroTik CRS326-24G-2S+RM", 2),
        ("NW0009", "Сетевое оборудование", "Huawei S5735-L24T4S", 500),
        ("NW0010", "Сетевое оборудование", "Zyxel GS1915-8EP", 1000),
    ];

    /// <summary>
    /// Применяет миграции и заполняет пустые таблицы начальными данными.
    /// Вызывается один раз при старте приложения.
    /// </summary>
    /// <param name="services">Корневой провайдер сервисов приложения.</param>
    /// <param name="ct">Токен отмены (останавливает инициализацию при завершении приложения).</param>
    /// <returns>Задача, завершающаяся после подготовки БД.</returns>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));

        await MigrateWithRetryAsync(db, logger, ct);

        // Блокировка защищает от двойного заполнения, если одновременно стартуют несколько экземпляров сервера.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SeedLockKey})", ct);

        if (!await db.EquipmentTypes.AnyAsync(ct))
        {
            db.EquipmentTypes.AddRange(DefaultTypes.Select(name => new EquipmentType { Name = name }));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Словарь типов техники заполнен: {Count} позиций", DefaultTypes.Length);
        }

        if (!await db.EquipmentItems.AnyAsync(ct))
        {
            var typeIds = await db.EquipmentTypes.ToDictionaryAsync(t => t.Name, t => t.Id, ct);
            var items = DefaultItems
                .Where(i => typeIds.ContainsKey(i.Type))
                .Select(i => new EquipmentItem
                {
                    InventoryNumber = i.Number,
                    Name = i.Name,
                    TypeId = typeIds[i.Type],
                    Room = i.Room,
                })
                .ToList();

            db.EquipmentItems.AddRange(items);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Добавлены демо-записи техники: {Count}", items.Count);
        }

        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Применяет миграции, повторяя попытку, пока PostgreSQL не станет доступен
    /// (например, контейнер БД еще запускается).
    /// </summary>
    /// <param name="db">Контекст базы данных.</param>
    /// <param name="logger">Логгер для сообщений о повторных попытках.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после применения миграций.</returns>
    private static async Task MigrateWithRetryAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // Создает БД, если ее нет, и применяет все неприменённые миграции из Data/Migrations.
                // EF Core сам берет блокировку, поэтому параллельный старт нескольких экземпляров безопасен.
                await db.Database.MigrateAsync(ct);
                return;
            }
            catch (NpgsqlException ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning("PostgreSQL недоступен (попытка {Attempt}/{Max}): {Message}", attempt, MaxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}
