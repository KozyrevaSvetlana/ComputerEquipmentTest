namespace ComputerEquipment.Api.Domain;

/// <summary>Тип техники — запись словаря (таблица <c>equipment_types</c>).</summary>
public sealed class EquipmentType
{
    /// <summary>Идентификатор типа.</summary>
    public int Id { get; set; }

    /// <summary>Название типа, до <see cref="EquipmentRules.TypeNameMaxLength"/> символов, уникально.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Техника этого типа.</summary>
    public List<EquipmentItem> Items { get; set; } = [];
}
