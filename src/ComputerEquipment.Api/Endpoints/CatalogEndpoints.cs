using ComputerEquipment.Api.Contracts;
using ComputerEquipment.Api.Data;
using ComputerEquipment.Api.Domain;
using ComputerEquipment.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComputerEquipment.Api.Endpoints;

/// <summary>
/// Minimal API каталога и словаря типов техники.
/// </summary>
public static class CatalogEndpoints
{
    /// <summary>
    /// Регистрирует маршруты <c>/api/catalog</c> и <c>/api/equipment-types</c>.
    /// </summary>
    /// <param name="app">Построитель маршрутов приложения.</param>
    /// <returns>Тот же построитель маршрутов — для цепочки вызовов.</returns>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog", GetCatalog);

        var types = app.MapGroup("/api/equipment-types");
        types.MapGet("/", GetTypes);
        types.MapPost("/", CreateType);
        types.MapDelete("/{id:int}", DeleteType);

        return app;
    }

    /// <summary>
    /// Возвращает каталог: типы техники с количеством единиц, общее число записей и лимит.
    /// Клиент строит по нему левую панель и счетчик «N из 2000».
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="options">Настройки модуля (лимит записей).</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>200 с <see cref="CatalogDto"/>.</returns>
    private static async Task<IResult> GetCatalog(AppDbContext context, IOptions<EquipmentOptions> options, CancellationToken ct)
    {
        var types = await LoadTypes(context, ct);
        return Results.Ok(new CatalogDto(types.Sum(t => t.Count), options.Value.MaxRecords, types));
    }

    /// <summary>Возвращает словарь типов техники, отсортированный по названию.</summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>200 со списком <see cref="EquipmentTypeDto"/>.</returns>
    private static async Task<IResult> GetTypes(AppDbContext context, CancellationToken ct) =>
        Results.Ok(await LoadTypes(context, ct));

    /// <summary>Добавляет тип техники в словарь.</summary>
    /// <param name="request">Название нового типа.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>201 с созданным типом, 400 при ошибке валидации, 409, если такой тип уже есть.</returns>
    private static async Task<IResult> CreateType(EquipmentTypeRequest request, AppDbContext context, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var errors = EquipmentRules.ValidateTypeName(name);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var entity = new EquipmentType { Name = name };
        context.EquipmentTypes.Add(entity);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(AppDbContext.TypeNameIndex))
        {
            return Problems.Conflict("Тип уже существует", $"Тип «{name}» уже есть в словаре.", "duplicate");
        }

        return Results.Created($"/api/equipment-types/{entity.Id}", new EquipmentTypeDto(entity.Id, entity.Name, 0));
    }

    /// <summary>
    /// Удаляет тип из словаря. Тип, к которому относится техника, удалить нельзя:
    /// это запрещает внешний ключ с <c>ON DELETE RESTRICT</c>.
    /// </summary>
    /// <param name="id">Идентификатор типа.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>204; 404, если типа нет; 409, если тип используется.</returns>
    private static async Task<IResult> DeleteType(int id, AppDbContext context, CancellationToken ct)
    {
        try
        {
            var deleted = await context.EquipmentTypes.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Problems.NotFound($"Тип с id={id} не найден.") : Results.NoContent();
        }
        catch (Exception ex) when (ex.IsForeignKeyViolation())
        {
            return Problems.Conflict("Тип используется", "Нельзя удалить тип, пока к нему относится техника.", "in-use");
        }
    }

    /// <summary>Загружает типы с количеством техники каждого типа (одним SQL-запросом).</summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>Список типов, отсортированный по названию.</returns>
    private static Task<List<EquipmentTypeDto>> LoadTypes(AppDbContext context, CancellationToken ct) =>
        context.EquipmentTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new EquipmentTypeDto(t.Id, t.Name, t.Items.Count))
            .ToListAsync(ct);
}
