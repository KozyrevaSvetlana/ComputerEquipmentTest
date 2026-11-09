using ComputerEquipment.Api.Contracts;
using ComputerEquipment.Api.Domain;

namespace ComputerEquipment.Api.Tests;

/// <summary>Тесты нормализации и валидации полей записи о технике.</summary>
public class EquipmentRulesTests
{
    /// <summary>Корректный запрос, от которого тесты меняют по одному полю.</summary>
    private static EquipmentRequest Valid(uint? version = null) =>
        new("PC0001", "Dell OptiPlex 7010", TypeId: 1, Room: 101, Version: version);

    /// <summary>Нормализация убирает пробелы по краям и переводит учетный номер в верхний регистр.</summary>
    [Fact]
    public void Normalize_TrimsAndUppercasesInventoryNumber()
    {
        var result = EquipmentRules.Normalize(new EquipmentRequest("  pc01a ", "  Монитор  ", 1, 5, null));

        Assert.Equal("PC01A", result.InventoryNumber);
        Assert.Equal("Монитор", result.Name);
    }

    /// <summary>Отсутствующие строки заменяются пустыми, чтобы валидация не падала на null.</summary>
    [Fact]
    public void Normalize_ReplacesNullStringsWithEmpty()
    {
        var result = EquipmentRules.Normalize(new EquipmentRequest(null, null, null, null, null));

        Assert.Equal(string.Empty, result.InventoryNumber);
        Assert.Equal(string.Empty, result.Name);
    }

    /// <summary>Нормализация не меняет тип, комнату и версию.</summary>
    [Fact]
    public void Normalize_KeepsOtherFields()
    {
        var result = EquipmentRules.Normalize(new EquipmentRequest("a", "b", 3, 7, 42));

        Assert.Equal(3, result.TypeId);
        Assert.Equal(7, result.Room);
        Assert.Equal(42u, result.Version);
    }

    /// <summary>Корректная запись проходит валидацию без ошибок.</summary>
    [Fact]
    public void Validate_ValidRequest_ReturnsNoErrors()
    {
        Assert.Empty(EquipmentRules.Validate(Valid(), requireVersion: false));
    }

    /// <summary>Учетный номер из цифр и латинских букв длиной от 1 до 32 символов допустим.</summary>
    [Theory]
    [InlineData("A")]
    [InlineData("0")]
    [InlineData("AB12cd34")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")] // ровно 32 символа
    public void Validate_AcceptsCorrectInventoryNumber(string number)
    {
        var errors = EquipmentRules.Validate(Valid() with { InventoryNumber = number }, requireVersion: false);

        Assert.DoesNotContain("inventoryNumber", errors.Keys);
    }

    /// <summary>Пустой номер, кириллица, спецсимволы и номер длиннее 32 символов отклоняются.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("ПК0001")]       // кириллица
    [InlineData("PC-0001")]      // дефис
    [InlineData("PC 0001")]      // пробел
    [InlineData("PC_0001")]      // подчеркивание
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456")] // 33 символа
    public void Validate_RejectsIncorrectInventoryNumber(string number)
    {
        var errors = EquipmentRules.Validate(Valid() with { InventoryNumber = number }, requireVersion: false);

        Assert.Contains("inventoryNumber", errors.Keys);
    }

    /// <summary>В наименовании допустимы любые символы.</summary>
    [Fact]
    public void Validate_AcceptsAnyCharactersInName()
    {
        var request = Valid() with { Name = "Монитор 27\" (каб. №5) — запасной; ½ & <тест>" };

        Assert.DoesNotContain("name", EquipmentRules.Validate(request, requireVersion: false).Keys);
    }

    /// <summary>Наименование ровно из 256 символов допустимо.</summary>
    [Fact]
    public void Validate_AcceptsNameOfMaxLength()
    {
        var request = Valid() with { Name = new string('x', EquipmentRules.NameMaxLength) };

        Assert.DoesNotContain("name", EquipmentRules.Validate(request, requireVersion: false).Keys);
    }

    /// <summary>Пустое наименование и наименование длиннее 256 символов отклоняются.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(EquipmentRules.NameMaxLength + 1)]
    public void Validate_RejectsEmptyOrTooLongName(int length)
    {
        var request = Valid() with { Name = new string('x', length) };

        Assert.Contains("name", EquipmentRules.Validate(request, requireVersion: false).Keys);
    }

    /// <summary>Тип техники обязателен, а его идентификатор должен быть положительным.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RejectsMissingOrInvalidTypeId(int? typeId)
    {
        var errors = EquipmentRules.Validate(Valid() with { TypeId = typeId }, requireVersion: false);

        Assert.Contains("typeId", errors.Keys);
    }

    /// <summary>Комнаты 1, 1000 и значения между ними допустимы.</summary>
    [Theory]
    [InlineData(EquipmentRules.RoomMin)]
    [InlineData(500)]
    [InlineData(EquipmentRules.RoomMax)]
    public void Validate_AcceptsRoomInRange(int room)
    {
        var errors = EquipmentRules.Validate(Valid() with { Room = room }, requireVersion: false);

        Assert.DoesNotContain("room", errors.Keys);
    }

    /// <summary>Комната обязательна и не может выходить за диапазон от 1 до 1000.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1001)]
    public void Validate_RejectsMissingOrOutOfRangeRoom(int? room)
    {
        var errors = EquipmentRules.Validate(Valid() with { Room = room }, requireVersion: false);

        Assert.Contains("room", errors.Keys);
    }

    /// <summary>При изменении записи версия обязательна — без нее нельзя проверить, не изменил ли запись другой пользователь.</summary>
    [Fact]
    public void Validate_RequiresVersionForUpdate()
    {
        Assert.Contains("version", EquipmentRules.Validate(Valid(version: null), requireVersion: true).Keys);
        Assert.DoesNotContain("version", EquipmentRules.Validate(Valid(version: 5), requireVersion: true).Keys);
    }

    /// <summary>При создании записи версия не нужна.</summary>
    [Fact]
    public void Validate_DoesNotRequireVersionForCreate()
    {
        Assert.DoesNotContain("version", EquipmentRules.Validate(Valid(version: null), requireVersion: false).Keys);
    }

    /// <summary>Валидация возвращает ошибки по всем неверным полям сразу, а не только по первому.</summary>
    [Fact]
    public void Validate_ReportsAllInvalidFieldsAtOnce()
    {
        var errors = EquipmentRules.Validate(new EquipmentRequest("", "", null, null, null), requireVersion: true);

        Assert.Equal(new[] { "inventoryNumber", "name", "room", "typeId", "version" }, errors.Keys.Order());
    }

    /// <summary>Непустое название типа допустимо.</summary>
    [Theory]
    [InlineData("Ноутбук")]
    [InlineData("x")]
    public void ValidateTypeName_AcceptsCorrectName(string name)
    {
        Assert.Empty(EquipmentRules.ValidateTypeName(name));
    }

    /// <summary>Название типа ровно из 128 символов допустимо.</summary>
    [Fact]
    public void ValidateTypeName_AcceptsNameOfMaxLength()
    {
        Assert.Empty(EquipmentRules.ValidateTypeName(new string('x', EquipmentRules.TypeNameMaxLength)));
    }

    /// <summary>Пустое название типа и название длиннее 128 символов отклоняются.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(EquipmentRules.TypeNameMaxLength + 1)]
    public void ValidateTypeName_RejectsEmptyOrTooLongName(int length)
    {
        Assert.Contains("name", EquipmentRules.ValidateTypeName(new string('x', length)).Keys);
    }
}
