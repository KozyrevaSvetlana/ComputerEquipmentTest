namespace ComputerEquipment.Api.Contracts;

/// <summary>Тело запроса на создание или изменение записи о технике.</summary>
/// <param name="InventoryNumber">Учетный номер.</param>
/// <param name="Name">Наименование.</param>
/// <param name="TypeId">Идентификатор типа техники.</param>
/// <param name="Room">Комната размещения.</param>
/// <param name="Version">
/// Версия строки, полученная при открытии карточки. Обязательна при изменении, при создании не передается.
/// </param>
public sealed record EquipmentRequest(string? InventoryNumber, string? Name, int? TypeId, int? Room, uint? Version);

/// <summary>Запись о технике в ответе API.</summary>
/// <param name="Id">Идентификатор записи.</param>
/// <param name="InventoryNumber">Учетный номер.</param>
/// <param name="Name">Наименование.</param>
/// <param name="TypeId">Идентификатор типа техники.</param>
/// <param name="TypeName">Название типа техники.</param>
/// <param name="Room">Комната размещения.</param>
/// <param name="Version">Версия строки для оптимистичной блокировки.</param>
public sealed record EquipmentDto(int Id, string InventoryNumber, string Name, int TypeId, string TypeName, int Room, uint Version);

/// <summary>Страница данных в формате, который ожидает DevExtreme CustomStore.</summary>
/// <typeparam name="T">Тип элементов.</typeparam>
/// <param name="Data">Элементы текущей страницы.</param>
/// <param name="TotalCount">Общее количество записей с учетом фильтров.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Data, int TotalCount);

/// <summary>Тело запроса на создание типа техники.</summary>
/// <param name="Name">Название типа.</param>
public sealed record EquipmentTypeRequest(string? Name);

/// <summary>Тип техники с количеством единиц.</summary>
/// <param name="Id">Идентификатор типа.</param>
/// <param name="Name">Название типа.</param>
/// <param name="Count">Сколько техники этого типа в базе.</param>
public sealed record EquipmentTypeDto(int Id, string Name, int Count);

/// <summary>Каталог: типы техники с количеством, общее число записей и лимит.</summary>
/// <param name="Total">Всего записей техники.</param>
/// <param name="Limit">Максимально допустимое число записей.</param>
/// <param name="Types">Типы техники.</param>
public sealed record CatalogDto(int Total, int Limit, IReadOnlyList<EquipmentTypeDto> Types);

/// <summary>Элемент сортировки DevExtreme: <c>{ "selector": "name", "desc": true }</c>.</summary>
/// <param name="Selector">Имя поля: <c>inventoryNumber</c>, <c>typeName</c>, <c>name</c> или <c>room</c>.</param>
/// <param name="Desc">Сортировать по убыванию.</param>
public sealed record SortItem(string? Selector, bool Desc);
