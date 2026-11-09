using System.Linq.Expressions;
using ComputerEquipment.Api.Contracts;
using ComputerEquipment.Api.Data;
using ComputerEquipment.Api.Domain;
using ComputerEquipment.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComputerEquipment.Api.Endpoints;

/// <summary>
/// Minimal API для работы с компьютерной техникой: список с пагинацией и сортировкой, просмотр,
/// создание, изменение и удаление записей.
/// </summary>
public static class EquipmentEndpoints
{
    /// <summary>
    /// Ключ advisory-блокировки PostgreSQL. Все вставки выполняются по очереди,
    /// чтобы параллельные запросы не превысили лимит записей.
    /// </summary>
    private const long CreateLockKey = 7_100_001;

    /// <summary>Проекция сущности в DTO; транслируется EF Core в SQL с JOIN на словарь типов.</summary>
    private static readonly Expression<Func<EquipmentItem, EquipmentDto>> ToDto = e =>
        new EquipmentDto(e.Id, e.InventoryNumber, e.Name, e.TypeId, e.Type.Name, e.Room, e.Version);

    /// <summary>
    /// Регистрирует маршруты <c>/api/equipment</c>.
    /// </summary>
    /// <param name="app">Построитель маршрутов приложения.</param>
    /// <returns>Тот же построитель маршрутов — для цепочки вызовов.</returns>
    public static IEndpointRouteBuilder MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/equipment");

        group.MapGet("/", GetPage);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);

        return app;
    }

    /// <summary>
    /// Возвращает страницу списка. Параметры совпадают с тем, что отправляет DevExtreme DataGrid
    /// при <c>remoteOperations</c>: <c>skip</c>, <c>take</c>, <c>sort</c>.
    /// </summary>
    /// <param name="skip">Сколько записей пропустить.</param>
    /// <param name="take">Размер страницы (не больше <see cref="EquipmentQuery.MaxPageSize"/>).</param>
    /// <param name="sort">Сортировка в формате DevExtreme: <c>[{"selector":"name","desc":false}]</c>.</param>
    /// <param name="typeId">Фильтр по типу техники; <see langword="null"/> — все типы.</param>
    /// <param name="search">Поиск по учетному номеру или наименованию без учета регистра.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>200 с <see cref="PagedResult{T}"/> или 400 при некорректной сортировке.</returns>
    private static async Task<IResult> GetPage(
        int? skip, int? take, string? sort, int? typeId, string? search,
        AppDbContext context, CancellationToken ct)
    {
        if (!EquipmentQuery.TryParseSort(sort, out var sortItems))
            return Problems.BadRequest("Параметр sort должен быть JSON-массивом вида [{\"selector\":\"name\",\"desc\":false}].");

        IQueryable<EquipmentItem> query = context.EquipmentItems.AsNoTracking();

        if (typeId is int typeIdValue)
            query = query.Where(e => e.TypeId == typeIdValue);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EquipmentQuery.EscapeLike(search.Trim())}%";
            query = query.Where(e => EF.Functions.ILike(e.InventoryNumber, pattern) || EF.Functions.ILike(e.Name, pattern));
        }

        var sorted = EquipmentQuery.ApplySorting(query, sortItems, out var unknownSelector);
        if (sorted is null)
            return Problems.BadRequest($"Сортировка по полю «{unknownSelector}» не поддерживается.");

        var (skipValue, takeValue) = EquipmentQuery.NormalizePaging(skip, take);

        var totalCount = await query.CountAsync(ct);

        var data = await sorted
            .Skip(skipValue)
            .Take(takeValue)
            .Select(ToDto)
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<EquipmentDto>(data, totalCount));
    }

    /// <summary>
    /// Возвращает одну запись. Клиент вызывает этот метод при открытии диалога редактирования,
    /// чтобы получить актуальные данные и версию строки.
    /// </summary>
    /// <param name="id">Идентификатор записи.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>200 с <see cref="EquipmentDto"/> или 404.</returns>
    private static async Task<IResult> GetById(int id, AppDbContext context, CancellationToken ct)
    {
        var dto = await FindDto(context, id, ct);
        return dto is null ? Problems.NotFound($"Запись с id={id} не найдена.") : Results.Ok(dto);
    }

    /// <summary>
    /// Создает запись о технике.
    /// </summary>
    /// <remarks>
    /// Подсчет записей и вставка выполняются в одной транзакции под advisory-блокировкой,
    /// поэтому лимит не превысят параллельные запросы. Уникальность учетного номера гарантирует
    /// уникальный индекс в БД: если два пользователя одновременно сохраняют один номер,
    /// второй получит 409.
    /// </remarks>
    /// <param name="request">Данные новой записи.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="options">Настройки модуля (лимит записей).</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>201 с созданной записью, 400 при ошибках валидации, 409 при дубле номера или достижении лимита.</returns>
    private static async Task<IResult> Create(
        EquipmentRequest request,
        AppDbContext context,
        IOptions<EquipmentOptions> options,
        CancellationToken ct)
    {
        var input = EquipmentRules.Normalize(request);
        var errors = EquipmentRules.Validate(input, requireVersion: false);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        if (!await context.EquipmentTypes.AnyAsync(x => x.Id == input.TypeId, ct))
            return TypeNotFound();

        // await using закрывает транзакцию при любом выходе из метода;
        // без CommitAsync это откат, и advisory-блокировка снимается вместе с ним.
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({CreateLockKey})", ct);

        var limit = options.Value.MaxRecords;
        if (await context.EquipmentItems.CountAsync(ct) >= limit)
            return Problems.Conflict("Достигнут лимит записей", $"В базе уже {limit} записей — это максимум.", "limit");

        var entity = new EquipmentItem
        {
            InventoryNumber = input.InventoryNumber!,
            Name = input.Name!,
            TypeId = input.TypeId!.Value,
            Room = input.Room!.Value,
        };
        context.EquipmentItems.Add(entity);

        try
        {
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(AppDbContext.InventoryNumberIndex))
        {
            // Проверку «есть ли такой номер» не делаем заранее: между проверкой и вставкой
            // другой пользователь мог успеть сохранить тот же номер. Решает уникальный индекс.
            return DuplicateNumber(entity.InventoryNumber);
        }

        var dto = await FindDto(context, entity.Id, ct);
        return Results.Created($"/api/equipment/{entity.Id}", dto);
    }

    /// <summary>
    /// Изменяет запись с проверкой версии (оптимистичная блокировка по столбцу <c>xmin</c>).
    /// </summary>
    /// <param name="id">Идентификатор записи.</param>
    /// <param name="request">Новые данные и версия, которую пользователь видел при открытии карточки.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>
    /// 200 с обновленной записью; 400 при ошибках валидации; 404, если запись удалена;
    /// 409, если запись успела изменить другой пользователь или номер уже занят.
    /// </returns>
    private static async Task<IResult> Update(
        int id,
        EquipmentRequest request,
        AppDbContext context,
        CancellationToken ct)
    {
        var input = EquipmentRules.Normalize(request);
        var errors = EquipmentRules.Validate(input, requireVersion: true);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var entity = await context.EquipmentItems.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null)
            return Problems.NotFound($"Запись с id={id} не найдена. Возможно, ее удалил другой пользователь.");

        if (!await context.EquipmentTypes.AnyAsync(x => x.Id == input.TypeId, ct))
            return TypeNotFound();

        // Версия, которую видел пользователь при открытии диалога. Если запись с тех пор изменили,
        // UPDATE ... WHERE xmin = @version не затронет строк и EF выбросит DbUpdateConcurrencyException.
        context.Entry(entity).Property(e => e.Version).OriginalValue = input.Version!.Value;

        entity.InventoryNumber = input.InventoryNumber!;
        entity.Name = input.Name!;
        entity.TypeId = input.TypeId!.Value;
        entity.Room = input.Room!.Value;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problems.Conflict(
                "Запись изменена другим пользователем",
                "Пока вы редактировали карточку, ее изменили или удалили. Откройте запись заново и повторите изменения.",
                "concurrency");
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(AppDbContext.InventoryNumberIndex))
        {
            return DuplicateNumber(entity.InventoryNumber);
        }

        return Results.Ok(await FindDto(context, id, ct));
    }

    /// <summary>
    /// Удаляет запись одним SQL-запросом DELETE без предварительной загрузки.
    /// </summary>
    /// <param name="id">Идентификатор записи.</param>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>204 или 404, если записи нет.</returns>
    private static async Task<IResult> Delete(int id, AppDbContext context, CancellationToken ct)
    {
        var deleted = await context.EquipmentItems.Where(e => e.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? Problems.NotFound($"Запись с id={id} не найдена.") : Results.NoContent();
    }

    /// <summary>Загружает запись в виде DTO вместе с названием типа.</summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="id">Идентификатор записи.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <returns>DTO или <see langword="null"/>, если записи нет.</returns>
    private static Task<EquipmentDto?> FindDto(AppDbContext context, int id, CancellationToken ct) =>
        context.EquipmentItems.AsNoTracking().Where(e => e.Id == id).Select(ToDto).FirstOrDefaultAsync(ct);

    /// <summary>Ответ 409 о занятом учетном номере.</summary>
    /// <param name="number">Учетный номер, который не удалось сохранить.</param>
    /// <returns>ProblemDetails с кодом <c>duplicate</c>.</returns>
    private static IResult DuplicateNumber(string number) =>
        Problems.Conflict(
            "Учетный номер уже занят",
            $"Запись с учетным номером «{number}» уже есть в базе. Возможно, ее только что добавил другой пользователь.",
            "duplicate");

    /// <summary>Ответ 400, когда указанного типа нет в словаре.</summary>
    /// <returns>ValidationProblemDetails с ошибкой по полю <c>typeId</c>.</returns>
    private static IResult TypeNotFound() =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["typeId"] = ["Такого типа техники нет в словаре. Обновите страницу."],
        });
}
