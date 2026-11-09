using ComputerEquipment.Api.Contracts;
using ComputerEquipment.Api.Data;
using ComputerEquipment.Api.Domain;

namespace ComputerEquipment.Api.Tests;

/// <summary>
/// Тесты начальных данных: если демо-запись нарушает правила, сервер упадет при первом запуске
/// на CHECK-ограничении или уникальном индексе. Эти тесты ловят такую ошибку заранее.
/// </summary>
public class SeedDataTests
{
    [Fact]
    public void DefaultTypes_AreBetween3And5AndUnique()
    {
        // По заданию в пустой словарь записывается от 3 до 5 позиций.
        Assert.InRange(DbInitializer.DefaultTypes.Length, 3, 5);
        Assert.Equal(DbInitializer.DefaultTypes.Length, DbInitializer.DefaultTypes.Distinct().Count());
    }

    [Fact]
    public void DefaultTypes_PassValidation()
    {
        Assert.All(DbInitializer.DefaultTypes, name => Assert.Empty(EquipmentRules.ValidateTypeName(name)));
    }

    [Fact]
    public void DefaultItems_Contain50Records()
    {
        Assert.Equal(50, DbInitializer.DefaultItems.Length);
    }

    [Fact]
    public void DefaultItems_HaveUniqueInventoryNumbers()
    {
        var numbers = DbInitializer.DefaultItems.Select(i => i.Number.ToUpperInvariant()).ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public void DefaultItems_ReferenceExistingTypes()
    {
        Assert.All(DbInitializer.DefaultItems, i => Assert.Contains(i.Type, DbInitializer.DefaultTypes));
    }

    [Fact]
    public void DefaultItems_PassValidation()
    {
        Assert.All(DbInitializer.DefaultItems, i =>
        {
            var request = EquipmentRules.Normalize(new EquipmentRequest(i.Number, i.Name, TypeId: 1, i.Room, Version: null));

            Assert.Empty(EquipmentRules.Validate(request, requireVersion: false));
            // В БД номер хранится уже в верхнем регистре — иначе сработает CHECK-ограничение.
            Assert.Equal(request.InventoryNumber, i.Number);
        });
    }
}
