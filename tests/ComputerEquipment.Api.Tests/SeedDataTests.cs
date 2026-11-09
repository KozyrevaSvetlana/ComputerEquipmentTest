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
    /// <summary>В словарь добавляется от 3 до 5 типов, как требует задание, и все они разные.</summary>
    [Fact]
    public void DefaultTypes_AreBetween3And5AndUnique()
    {
        // По заданию в пустой словарь записывается от 3 до 5 позиций.
        Assert.InRange(DbInitializer.DefaultTypes.Length, 3, 5);
        Assert.Equal(DbInitializer.DefaultTypes.Length, DbInitializer.DefaultTypes.Distinct().Count());
    }

    /// <summary>Названия типов по умолчанию проходят валидацию.</summary>
    [Fact]
    public void DefaultTypes_PassValidation()
    {
        Assert.All(DbInitializer.DefaultTypes, name => Assert.Empty(EquipmentRules.ValidateTypeName(name)));
    }

    /// <summary>При первом запуске добавляется 50 демо-записей.</summary>
    [Fact]
    public void DefaultItems_Contain50Records()
    {
        Assert.Equal(50, DbInitializer.DefaultItems.Length);
    }

    /// <summary>Учетные номера демо-записей не повторяются, иначе заполнение упадет на уникальном индексе.</summary>
    [Fact]
    public void DefaultItems_HaveUniqueInventoryNumbers()
    {
        var numbers = DbInitializer.DefaultItems.Select(i => i.Number.ToUpperInvariant()).ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    /// <summary>Каждая демо-запись ссылается на тип из словаря по умолчанию.</summary>
    [Fact]
    public void DefaultItems_ReferenceExistingTypes()
    {
        Assert.All(DbInitializer.DefaultItems, i => Assert.Contains(i.Type, DbInitializer.DefaultTypes));
    }

    /// <summary>Все демо-записи проходят валидацию и уже записаны в верхнем регистре, как того требует CHECK-ограничение.</summary>
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
