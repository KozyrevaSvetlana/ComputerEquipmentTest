using System.Text.RegularExpressions;
using ComputerEquipment.Api.Contracts;

namespace ComputerEquipment.Api.Domain;

/// <summary>Правила предметной области: ограничения полей, нормализация и валидация.</summary>
public static partial class EquipmentRules
{
    /// <summary>Максимальная длина учетного номера.</summary>
    public const int InventoryNumberMaxLength = 32;

    /// <summary>Максимальная длина наименования техники.</summary>
    public const int NameMaxLength = 256;

    /// <summary>Максимальная длина названия типа техники.</summary>
    public const int TypeNameMaxLength = 128;

    /// <summary>Минимальный номер комнаты.</summary>
    public const int RoomMin = 1;

    /// <summary>Максимальный номер комнаты.</summary>
    public const int RoomMax = 1000;

    /// <summary>Учетный номер: только цифры и латинские буквы, от 1 до 32 символов.</summary>
    [GeneratedRegex("^[0-9A-Za-z]{1,32}$")]
    private static partial Regex InventoryNumberRegex();

    /// <summary>
    /// Нормализует ввод: обрезает пробелы и приводит учетный номер к верхнему регистру,
    /// чтобы «pc01» и «PC01» считались одним номером.
    /// </summary>
    /// <param name="request">Запрос от клиента.</param>
    /// <returns>Копия запроса с нормализованными строками; <see langword="null"/> заменяется пустой строкой.</returns>
    public static EquipmentRequest Normalize(EquipmentRequest request) => request with
    {
        InventoryNumber = request.InventoryNumber?.Trim().ToUpperInvariant() ?? string.Empty,
        Name = request.Name?.Trim() ?? string.Empty,
    };

    /// <summary>
    /// Проверяет поля записи о технике. Вызывать после <see cref="Normalize"/>.
    /// </summary>
    /// <param name="r">Нормализованный запрос.</param>
    /// <param name="requireVersion"><see langword="true"/> для изменения: версия строки обязательна.</param>
    /// <returns>
    /// Ошибки по полям (ключи в camelCase, как в JSON). Пустой словарь означает, что данные корректны.
    /// </returns>
    public static Dictionary<string, string[]> Validate(EquipmentRequest r, bool requireVersion)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrEmpty(r.InventoryNumber))
            errors["inventoryNumber"] = ["Укажите учетный номер."];
        else if (!InventoryNumberRegex().IsMatch(r.InventoryNumber))
            errors["inventoryNumber"] = [$"Только цифры и латинские буквы, не длиннее {InventoryNumberMaxLength} символов."];

        if (string.IsNullOrEmpty(r.Name))
            errors["name"] = ["Укажите наименование."];
        else if (r.Name.Length > NameMaxLength)
            errors["name"] = [$"Наименование не длиннее {NameMaxLength} символов."];

        if (r.TypeId is null or <= 0)
            errors["typeId"] = ["Выберите тип техники."];

        if (r.Room is null)
            errors["room"] = ["Укажите комнату размещения."];
        else if (r.Room is < RoomMin or > RoomMax)
            errors["room"] = [$"Комната — целое число от {RoomMin} до {RoomMax}."];

        if (requireVersion && r.Version is null)
            errors["version"] = ["Не передана версия записи."];

        return errors;
    }

    /// <summary>Проверяет название типа техники.</summary>
    /// <param name="name">Название без пробелов по краям.</param>
    /// <returns>Ошибки по полю <c>name</c>; пустой словарь, если название корректно.</returns>
    public static Dictionary<string, string[]> ValidateTypeName(string name)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(name))
            errors["name"] = ["Укажите название типа."];
        else if (name.Length > TypeNameMaxLength)
            errors["name"] = [$"Название типа не длиннее {TypeNameMaxLength} символов."];
        return errors;
    }
}
