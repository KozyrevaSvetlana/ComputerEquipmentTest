using ComputerEquipment.Api.Data;
using ComputerEquipment.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ComputerEquipment.Api.Tests;

/// <summary>
/// Тесты модели EF Core. Модель строится без подключения к PostgreSQL:
/// строка подключения нужна только для выбора провайдера, запросы в базу не отправляются.
/// </summary>
public sealed class AppDbContextModelTests : IDisposable
{
    private readonly AppDbContext _context = new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_tests")
            .Options);

    /// <summary>
    /// Полная (design-time) модель. В <c>context.Model</c> лежит облегченная runtime-модель:
    /// в ней нет части настроек схемы, например CHECK-ограничений.
    /// </summary>
    private IModel DesignModel => _context.GetService<IDesignTimeModel>().Model;

    private IEntityType Equipment => DesignModel.FindEntityType(typeof(EquipmentItem))!;

    private IEntityType Types => DesignModel.FindEntityType(typeof(EquipmentType))!;

    /// <inheritdoc />
    public void Dispose() => _context.Dispose();

    /// <summary>Миграции в Data/Migrations соответствуют текущей модели: модель не меняли без новой миграции.</summary>
    [Fact]
    public void Migrations_MatchCurrentModel()
    {
        // Если тест упал, модель изменили без новой миграции:
        // dotnet ef migrations add <Имя> --project src/ComputerEquipment.Api --output-dir Data/Migrations
        Assert.False(_context.Database.HasPendingModelChanges());
    }

    /// <summary>Сущности отображаются на таблицы <c>equipment</c> и <c>equipment_types</c>.</summary>
    [Fact]
    public void Tables_HaveExpectedNames()
    {
        Assert.Equal("equipment", Equipment.GetTableName());
        Assert.Equal("equipment_types", Types.GetTableName());
    }

    /// <summary>На учетном номере есть уникальный индекс с ожидаемым именем — на нем держится защита от одновременного ввода одного номера.</summary>
    [Fact]
    public void InventoryNumber_HasUniqueIndex()
    {
        // На этом индексе держится защита от одновременного ввода одного номера.
        var index = Assert.Single(Equipment.GetIndexes(),
            i => i.Properties.Single().Name == nameof(EquipmentItem.InventoryNumber));

        Assert.True(index.IsUnique);
        Assert.Equal(AppDbContext.InventoryNumberIndex, index.GetDatabaseName());
    }

    /// <summary>Название типа уникально: в словаре не может быть двух одинаковых типов.</summary>
    [Fact]
    public void TypeName_HasUniqueIndex()
    {
        var index = Assert.Single(Types.GetIndexes());

        Assert.True(index.IsUnique);
        Assert.Equal(AppDbContext.TypeNameIndex, index.GetDatabaseName());
    }

    /// <summary>Строковые поля техники обязательны и ограничены по длине так же, как в правилах валидации.</summary>
    [Theory]
    [InlineData(nameof(EquipmentItem.InventoryNumber), EquipmentRules.InventoryNumberMaxLength)]
    [InlineData(nameof(EquipmentItem.Name), EquipmentRules.NameMaxLength)]
    public void EquipmentStrings_HaveMaxLength(string property, int maxLength)
    {
        var p = Equipment.FindProperty(property)!;

        Assert.Equal(maxLength, p.GetMaxLength());
        Assert.False(p.IsNullable);
    }

    /// <summary>Название типа ограничено 128 символами, как требует задание.</summary>
    [Fact]
    public void TypeName_HasMaxLength()
    {
        Assert.Equal(EquipmentRules.TypeNameMaxLength, Types.FindProperty(nameof(EquipmentType.Name))!.GetMaxLength());
    }

    /// <summary>Версия строки отображается на системный столбец <c>xmin</c> и используется как маркер оптимистичной блокировки.</summary>
    [Fact]
    public void Version_IsXminConcurrencyToken()
    {
        var version = Equipment.FindProperty(nameof(EquipmentItem.Version))!;

        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    /// <summary>В БД заданы CHECK-ограничения на формат учетного номера и диапазон комнаты.</summary>
    [Fact]
    public void CheckConstraints_AreDefined()
    {
        var names = Equipment.GetCheckConstraints().Select(c => c.Name).Order().ToArray();

        Assert.Equal(new[] { "ck_equipment_inventory_number", "ck_equipment_room" }, names);
    }

    /// <summary>Внешний ключ на словарь типов обязателен и запрещает удалять тип, к которому относится техника.</summary>
    [Fact]
    public void DeletingUsedType_IsRestricted()
    {
        var fk = Assert.Single(Equipment.GetForeignKeys());

        Assert.Equal(typeof(EquipmentType), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.True(fk.IsRequired);
    }
}
