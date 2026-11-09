namespace ComputerEquipment.Api.Infrastructure;

/// <summary>
/// Настройки модуля учета техники. Привязываются к секции <c>Equipment</c> конфигурации.
/// </summary>
public sealed class EquipmentOptions
{
    /// <summary>Имя секции в <c>appsettings.json</c>.</summary>
    public const string Section = "Equipment";

    /// <summary>Максимальное количество записей техники в БД. По умолчанию 2000.</summary>
    public int MaxRecords { get; set; } = 2000;
}
