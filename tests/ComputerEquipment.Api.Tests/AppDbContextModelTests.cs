using ComputerEquipment.Api.Data;
using ComputerEquipment.Api.Domain;
using Microsoft.EntityFrameworkCore;
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

    private IEntityType Equipment => _context.Model.FindEntityType(typeof(EquipmentItem))!;

    private IEntityType Types => _context.Model.FindEntityType(typeof(EquipmentType))!;

    /// <inheritdoc />
    public void Dispose() => _context.Dispose();

    [Fact]
    public void Migrations_MatchCurrentModel()
    {
        // Если тест упал, модель изменили без новой миграции:
        // dotnet ef migrations add <Имя> --project src/ComputerEquipment.Api --output-dir Data/Migrations
        Assert.False(_context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Tables_HaveExpectedNames()
    {
        Assert.Equal("equipment", Equipment.GetTableName());
        Assert.Equal("equipment_types", Types.GetTableName());
    }

    [Fact]
    public void InventoryNumber_HasUniqueIndex()
    {
        // На этом индексе держится защита от одновременного ввода одного номера.
        var index = Assert.Single(Equipment.GetIndexes(),
            i => i.Properties.Single().Name == nameof(EquipmentItem.InventoryNumber));

        Assert.True(index.IsUnique);
        Assert.Equal(AppDbContext.InventoryNumberIndex, index.GetDatabaseName());
    }

    [Fact]
    public void TypeName_HasUniqueIndex()
    {
        var index = Assert.Single(Types.GetIndexes());

        Assert.True(index.IsUnique);
        Assert.Equal(AppDbContext.TypeNameIndex, index.GetDatabaseName());
    }

    [Theory]
    [InlineData(nameof(EquipmentItem.InventoryNumber), EquipmentRules.InventoryNumberMaxLength)]
    [InlineData(nameof(EquipmentItem.Name), EquipmentRules.NameMaxLength)]
    public void EquipmentStrings_HaveMaxLength(string property, int maxLength)
    {
        var p = Equipment.FindProperty(property)!;

        Assert.Equal(maxLength, p.GetMaxLength());
        Assert.False(p.IsNullable);
    }

    [Fact]
    public void TypeName_HasMaxLength()
    {
        Assert.Equal(EquipmentRules.TypeNameMaxLength, Types.FindProperty(nameof(EquipmentType.Name))!.GetMaxLength());
    }

    [Fact]
    public void Version_IsXminConcurrencyToken()
    {
        var version = Equipment.FindProperty(nameof(EquipmentItem.Version))!;

        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    [Fact]
    public void CheckConstraints_AreDefined()
    {
        var names = Equipment.GetCheckConstraints().Select(c => c.Name).Order().ToArray();

        Assert.Equal(new[] { "ck_equipment_inventory_number", "ck_equipment_room" }, names);
    }

    [Fact]
    public void DeletingUsedType_IsRestricted()
    {
        var fk = Assert.Single(Equipment.GetForeignKeys());

        Assert.Equal(typeof(EquipmentType), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.True(fk.IsRequired);
    }
}
