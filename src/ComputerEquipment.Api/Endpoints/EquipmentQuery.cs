using System.Linq.Expressions;
using System.Text.Json;
using ComputerEquipment.Api.Contracts;
using ComputerEquipment.Api.Domain;

namespace ComputerEquipment.Api.Endpoints;

/// <summary>
/// Построение запроса списка техники: разбор параметра сортировки DevExtreme,
/// сортировка и пагинация.
/// </summary>
/// <remarks>
/// Логика вынесена из эндпоинта, чтобы ее можно было проверять юнит-тестами
/// на обычных коллекциях (<see cref="Queryable.AsQueryable{TElement}(IEnumerable{TElement})"/>) без базы данных.
/// </remarks>
internal static class EquipmentQuery
{
    /// <summary>Размер страницы, если клиент его не передал.</summary>
    public const int DefaultPageSize = 10;

    /// <summary>Максимальный размер страницы: защищает сервер от запроса всех записей разом.</summary>
    public const int MaxPageSize = 100;

    private static readonly JsonSerializerOptions SortJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Разбирает параметр <c>sort</c> в формате DevExtreme: <c>[{"selector":"name","desc":false}]</c>.
    /// </summary>
    /// <param name="sort">JSON-строка из запроса. Пустая строка или <see langword="null"/> означает «без сортировки».</param>
    /// <param name="items">Разобранные элементы сортировки; при ошибке — пустой список.</param>
    /// <returns><see langword="true"/>, если строка корректна; иначе <see langword="false"/>.</returns>
    public static bool TryParseSort(string? sort, out IReadOnlyList<SortItem> items)
    {
        items = [];
        if (string.IsNullOrWhiteSpace(sort))
            return true;

        try
        {
            items = JsonSerializer.Deserialize<List<SortItem>>(sort, SortJsonOptions) ?? [];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Применяет сортировку по списку полей. Последним всегда добавляется сортировка по <see cref="EquipmentItem.Id"/>,
    /// чтобы порядок был стабильным и строки с одинаковыми значениями не «прыгали» между страницами.
    /// </summary>
    /// <param name="source">Исходный запрос.</param>
    /// <param name="sortItems">Поля сортировки в порядке приоритета. Если список пуст, сортировка идет по учетному номеру.</param>
    /// <param name="unknownSelector">Имя неподдерживаемого поля, если оно встретилось; иначе <see langword="null"/>.</param>
    /// <returns>Отсортированный запрос или <see langword="null"/>, если встретилось неподдерживаемое поле.</returns>
    public static IOrderedQueryable<EquipmentItem>? ApplySorting(
        IQueryable<EquipmentItem> source,
        IReadOnlyList<SortItem> sortItems,
        out string? unknownSelector)
    {
        unknownSelector = null;
        IOrderedQueryable<EquipmentItem>? ordered = null;

        foreach (var item in sortItems)
        {
            ordered = item.Selector switch
            {
                "inventoryNumber" => ApplyOrder(source, ordered, e => e.InventoryNumber, item.Desc),
                "typeName" => ApplyOrder(source, ordered, e => e.Type.Name, item.Desc),
                "name" => ApplyOrder(source, ordered, e => e.Name, item.Desc),
                "room" => ApplyOrder(source, ordered, e => e.Room, item.Desc),
                _ => null,
            };

            if (ordered is null)
            {
                unknownSelector = item.Selector ?? string.Empty;
                return null;
            }
        }

        return (ordered ?? source.OrderBy(e => e.InventoryNumber)).ThenBy(e => e.Id);
    }

    /// <summary>
    /// Приводит параметры страницы к допустимым значениям.
    /// </summary>
    /// <param name="skip">Сколько записей пропустить; отрицательное значение и <see langword="null"/> дают 0.</param>
    /// <param name="take">Размер страницы; ограничивается диапазоном от 1 до <see cref="MaxPageSize"/>.</param>
    /// <returns>Корректные значения skip и take.</returns>
    public static (int Skip, int Take) NormalizePaging(int? skip, int? take) =>
        (Math.Max(skip ?? 0, 0), Math.Clamp(take ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Экранирует спецсимволы шаблона LIKE (<c>%</c>, <c>_</c>, <c>\</c>), чтобы пользовательский ввод
    /// искался как обычный текст.
    /// </summary>
    /// <param name="value">Строка поиска.</param>
    /// <returns>Строка, безопасная для подстановки в шаблон LIKE/ILIKE.</returns>
    public static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>Добавляет первый или очередной уровень сортировки.</summary>
    /// <typeparam name="TKey">Тип поля сортировки.</typeparam>
    /// <param name="source">Исходный запрос (используется для первого уровня).</param>
    /// <param name="ordered">Уже отсортированный запрос или <see langword="null"/>, если это первый уровень.</param>
    /// <param name="key">Выражение, выбирающее поле.</param>
    /// <param name="desc">Сортировать по убыванию.</param>
    /// <returns>Запрос с добавленным уровнем сортировки.</returns>
    private static IOrderedQueryable<EquipmentItem> ApplyOrder<TKey>(
        IQueryable<EquipmentItem> source,
        IOrderedQueryable<EquipmentItem>? ordered,
        Expression<Func<EquipmentItem, TKey>> key,
        bool desc)
    {
        if (ordered is null)
            return desc ? source.OrderByDescending(key) : source.OrderBy(key);

        return desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
    }
}
