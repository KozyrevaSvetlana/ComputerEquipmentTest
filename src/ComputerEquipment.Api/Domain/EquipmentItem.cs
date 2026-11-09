namespace ComputerEquipment.Api.Domain;

/// <summary>Единица компьютерной техники (таблица <c>equipment</c>).</summary>
public sealed class EquipmentItem
{
    /// <summary>Идентификатор записи.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Учетный номер: цифры и латинские буквы, до <see cref="EquipmentRules.InventoryNumberMaxLength"/> символов.
    /// Хранится в верхнем регистре и уникален.
    /// </summary>
    public string InventoryNumber { get; set; } = string.Empty;

    /// <summary>Наименование, до <see cref="EquipmentRules.NameMaxLength"/> символов.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Идентификатор типа техники из словаря.</summary>
    public int TypeId { get; set; }

    /// <summary>Тип техники.</summary>
    public EquipmentType Type { get; set; } = null!;

    /// <summary>Комната размещения: от <see cref="EquipmentRules.RoomMin"/> до <see cref="EquipmentRules.RoomMax"/>.</summary>
    public int Room { get; set; }

    /// <summary>
    /// Версия строки — системный столбец PostgreSQL <c>xmin</c>. Меняется при каждом изменении строки
    /// и используется для оптимистичной блокировки.
    /// </summary>
    public uint Version { get; set; }
}
