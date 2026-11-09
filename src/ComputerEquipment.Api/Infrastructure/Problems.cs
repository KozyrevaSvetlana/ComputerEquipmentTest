using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComputerEquipment.Api.Infrastructure;

/// <summary>
/// Единые ответы об ошибках в формате ProblemDetails (RFC 9457) и распознавание ошибок PostgreSQL.
/// </summary>
public static class Problems
{
    /// <summary>
    /// Ответ 409 Conflict с машиночитаемым кодом в поле <c>code</c>.
    /// </summary>
    /// <param name="title">Короткий заголовок ошибки.</param>
    /// <param name="detail">Подробное сообщение для пользователя.</param>
    /// <param name="code">Код конфликта: <c>duplicate</c>, <c>concurrency</c>, <c>limit</c>, <c>in-use</c>.</param>
    /// <returns>Результат с ProblemDetails.</returns>
    public static IResult Conflict(string title, string detail, string code) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    /// <summary>Ответ 404 Not Found.</summary>
    /// <param name="detail">Что именно не найдено.</param>
    /// <returns>Результат с ProblemDetails.</returns>
    public static IResult NotFound(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Не найдено", detail: detail);

    /// <summary>Ответ 400 Bad Request для ошибок, не относящихся к конкретному полю.</summary>
    /// <param name="detail">Описание ошибки.</param>
    /// <returns>Результат с ProblemDetails.</returns>
    public static IResult BadRequest(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Некорректный запрос", detail: detail);

    /// <summary>
    /// Проверяет, что сохранение упало из-за нарушения конкретного уникального индекса
    /// (код PostgreSQL <c>23505</c>).
    /// </summary>
    /// <param name="ex">Исключение EF Core.</param>
    /// <param name="constraintName">Имя индекса или ограничения в БД.</param>
    /// <returns><see langword="true"/>, если нарушен именно этот индекс.</returns>
    public static bool IsUniqueViolation(this DbUpdateException ex, string constraintName) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == constraintName;

    /// <summary>
    /// Проверяет, что операция упала из-за нарушения внешнего ключа (код PostgreSQL <c>23503</c>).
    /// </summary>
    /// <remarks>
    /// <c>SaveChanges</c> оборачивает ошибку PostgreSQL в <see cref="DbUpdateException"/>,
    /// а <c>ExecuteDelete</c> выбрасывает <see cref="PostgresException"/> напрямую — метод понимает оба случая.
    /// </remarks>
    /// <param name="ex">Исключение.</param>
    /// <returns><see langword="true"/>, если нарушен внешний ключ.</returns>
    public static bool IsForeignKeyViolation(this Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException)
            is { SqlState: PostgresErrorCodes.ForeignKeyViolation };
}
