using ComputerEquipment.Api.Contracts;
using ComputerEquipment.Api.Domain;
using ComputerEquipment.Api.Endpoints;

namespace ComputerEquipment.Api.Tests;

/// <summary>
/// Тесты разбора сортировки, сортировки и пагинации списка.
/// Запросы выполняются на обычной коллекции через <c>AsQueryable()</c>, без базы данных.
/// </summary>
public class EquipmentQueryTests
{
    private static readonly EquipmentType Laptop = new() { Id = 1, Name = "Ноутбук" };
    private static readonly EquipmentType Monitor = new() { Id = 2, Name = "Монитор" };

    /// <summary>Создает запись о технике для теста.</summary>
    private static EquipmentItem Item(int id, string number, string name, EquipmentType type, int room) =>
        new() { Id = id, InventoryNumber = number, Name = name, Type = type, TypeId = type.Id, Room = room };

    /// <summary>Набор, в котором у разных записей совпадают комнаты и типы — чтобы проверять порядок при равенстве.</summary>
    private static IQueryable<EquipmentItem> Sample() => new[]
    {
        Item(1, "NB0002", "Lenovo ThinkPad", Laptop, 205),
        Item(2, "MN0001", "Samsung S24", Monitor, 101),
        Item(3, "NB0001", "ASUS ExpertBook", Laptop, 101),
        Item(4, "MN0002", "Philips 243V", Monitor, 310),
    }.AsQueryable();

    /// <summary>Возвращает идентификаторы записей в порядке выдачи запроса.</summary>
    private static IReadOnlyList<int> Ids(IQueryable<EquipmentItem> query) => query.Select(e => e.Id).ToList();

    /// <summary>Сортирует тестовый набор и проверяет, что все поля сортировки известны.</summary>
    private static IOrderedQueryable<EquipmentItem> Sort(params SortItem[] items)
    {
        var sorted = EquipmentQuery.ApplySorting(Sample(), items, out var unknown);
        Assert.Null(unknown);
        Assert.NotNull(sorted);
        return sorted;
    }

    // ---------- TryParseSort ----------

    /// <summary>Пустой или отсутствующий параметр <c>sort</c> считается корректным и означает «без сортировки».</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    public void TryParseSort_EmptyInput_ReturnsEmptyList(string? sort)
    {
        Assert.True(EquipmentQuery.TryParseSort(sort, out var items));
        Assert.Empty(items);
    }

    /// <summary>Сортировка в формате DevExtreme разбирается в список полей с направлением.</summary>
    [Fact]
    public void TryParseSort_ParsesDevExtremeFormat()
    {
        var ok = EquipmentQuery.TryParseSort(
            """[{"selector":"typeName","desc":true},{"selector":"room","desc":false}]""", out var items);

        Assert.True(ok);
        Assert.Equal(new[] { new SortItem("typeName", true), new SortItem("room", false) }, items);
    }

    /// <summary>Если направление не указано, сортировка идет по возрастанию.</summary>
    [Fact]
    public void TryParseSort_MissingDesc_MeansAscending()
    {
        Assert.True(EquipmentQuery.TryParseSort("""[{"selector":"name"}]""", out var items));
        Assert.False(Assert.Single(items).Desc);
    }

    /// <summary>Некорректный JSON в <c>sort</c> не приводит к исключению: метод возвращает false.</summary>
    [Theory]
    [InlineData("name")]                   // не JSON
    [InlineData("""{"selector":"name"}""")] // объект вместо массива
    [InlineData("[{")]                     // оборванный JSON
    public void TryParseSort_InvalidJson_ReturnsFalse(string sort)
    {
        Assert.False(EquipmentQuery.TryParseSort(sort, out var items));
        Assert.Empty(items);
    }

    // ---------- ApplySorting ----------

    /// <summary>Без явной сортировки список упорядочен по учетному номеру.</summary>
    [Fact]
    public void ApplySorting_WithoutSort_OrdersByInventoryNumber()
    {
        Assert.Equal(new[] { 2, 4, 3, 1 }, Ids(Sort()));
    }

    /// <summary>Сортировка по одной колонке работает в обоих направлениях.</summary>
    [Theory]
    [InlineData("inventoryNumber", false, new[] { 2, 4, 3, 1 })]
    [InlineData("inventoryNumber", true, new[] { 1, 3, 4, 2 })]
    [InlineData("name", false, new[] { 3, 1, 4, 2 })]
    [InlineData("name", true, new[] { 2, 4, 1, 3 })]
    public void ApplySorting_BySingleField(string selector, bool desc, int[] expectedIds)
    {
        Assert.Equal(expectedIds, Ids(Sort(new SortItem(selector, desc))));
    }

    /// <summary>Колонка «Тип техники» сортируется по названию типа из словаря, а не по его идентификатору.</summary>
    [Fact]
    public void ApplySorting_ByTypeName_UsesTypeNavigation()
    {
        // «Монитор» < «Ноутбук»; внутри типа порядок по Id.
        Assert.Equal(new[] { 2, 4, 1, 3 }, Ids(Sort(new SortItem("typeName", false))));
    }

    /// <summary>При равных значениях порядок определяется идентификатором, поэтому записи не перескакивают между страницами.</summary>
    [Fact]
    public void ApplySorting_EqualValues_AreOrderedById()
    {
        // У записей 2 и 3 одна комната (101): порядок между ними задает Id — так страницы не «прыгают».
        Assert.Equal(new[] { 2, 3, 1, 4 }, Ids(Sort(new SortItem("room", false))));
        Assert.Equal(new[] { 4, 1, 2, 3 }, Ids(Sort(new SortItem("room", true))));
    }

    /// <summary>При сортировке по нескольким колонкам первая колонка главная, следующие уточняют порядок.</summary>
    [Fact]
    public void ApplySorting_ByMultipleFields_RespectsPriority()
    {
        // Сначала тип по убыванию (Ноутбук, Монитор), внутри типа — комната по возрастанию.
        var ids = Ids(Sort(new SortItem("typeName", true), new SortItem("room", false)));

        Assert.Equal(new[] { 3, 1, 2, 4 }, ids);
    }

    /// <summary>Неизвестное поле сортировки отклоняется, и метод сообщает его имя.</summary>
    [Theory]
    [InlineData("password")]
    [InlineData("Name")] // селекторы чувствительны к регистру, как в JSON от клиента
    [InlineData("")]
    public void ApplySorting_UnknownSelector_ReturnsNull(string selector)
    {
        var sorted = EquipmentQuery.ApplySorting(Sample(), [new SortItem(selector, false)], out var unknown);

        Assert.Null(sorted);
        Assert.Equal(selector, unknown);
    }

    /// <summary>Элемент сортировки без имени поля отклоняется.</summary>
    [Fact]
    public void ApplySorting_NullSelector_ReturnsNull()
    {
        var sorted = EquipmentQuery.ApplySorting(Sample(), [new SortItem(null, false)], out var unknown);

        Assert.Null(sorted);
        Assert.Equal(string.Empty, unknown);
    }

    /// <summary>Неизвестное поле отклоняется, даже если перед ним стоит корректное.</summary>
    [Fact]
    public void ApplySorting_UnknownSelectorAfterValidOne_ReturnsNull()
    {
        var sorted = EquipmentQuery.ApplySorting(
            Sample(), [new SortItem("room", false), new SortItem("unknown", false)], out var unknown);

        Assert.Null(sorted);
        Assert.Equal("unknown", unknown);
    }

    // ---------- NormalizePaging ----------

    /// <summary>Параметры страницы приводятся к допустимым значениям: skip не меньше 0, take от 1 до 100.</summary>
    [Theory]
    [InlineData(null, null, 0, EquipmentQuery.DefaultPageSize)]
    [InlineData(40, 20, 40, 20)]
    [InlineData(-5, 10, 0, 10)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, -3, 0, 1)]
    [InlineData(0, 1000, 0, EquipmentQuery.MaxPageSize)]
    public void NormalizePaging_ClampsValues(int? skip, int? take, int expectedSkip, int expectedTake)
    {
        Assert.Equal((expectedSkip, expectedTake), EquipmentQuery.NormalizePaging(skip, take));
    }

    /// <summary>Сортировка и пагинация вместе возвращают нужную страницу: из 50 записей вторая страница по 20 — это записи с 21-й по 40-ю.</summary>
    [Fact]
    public void Paging_ReturnsRequestedPageOfSortedData()
    {
        // 50 записей, как в демо-данных: вторая страница по 20 — это записи с 21-й по 40-ю.
        var items = Enumerable.Range(1, 50)
            .Select(i => Item(i, $"PC{i:0000}", $"ПК {i}", Laptop, room: i))
            .AsQueryable();

        var sorted = EquipmentQuery.ApplySorting(items, [], out _)!;
        var (skip, take) = EquipmentQuery.NormalizePaging(skip: 20, take: 20);
        var page = sorted.Skip(skip).Take(take).Select(e => e.Id).ToList();

        Assert.Equal(Enumerable.Range(21, 20), page);
    }

    // ---------- EscapeLike ----------

    /// <summary>Спецсимволы LIKE экранируются, и поиск по «50%» ищет именно этот текст.</summary>
    [Theory]
    [InlineData("PC01", "PC01")]
    [InlineData("50%", "50\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("c:\\temp", "c:\\\\temp")]
    [InlineData("%_\\", "\\%\\_\\\\")]
    public void EscapeLike_EscapesPatternCharacters(string input, string expected)
    {
        Assert.Equal(expected, EquipmentQuery.EscapeLike(input));
    }
}
