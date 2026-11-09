using ComputerEquipment.Api.Data;
using ComputerEquipment.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComputerEquipment.Api.Tests;

/// <summary>
/// Тесты распознавания ошибок PostgreSQL и ответов ProblemDetails.
/// Именно по этим проверкам сервер понимает, что два пользователя ввели один номер.
/// </summary>
public class ProblemsTests
{
    /// <summary>Создает исключение PostgreSQL с нужным кодом и именем ограничения.</summary>
    private static PostgresException Pg(string sqlState, string? constraintName = null) =>
        new("test error", "ERROR", "ERROR", sqlState, constraintName: constraintName);

    /// <summary>Так EF Core оборачивает ошибку базы при SaveChanges.</summary>
    private static DbUpdateException Wrap(Exception inner) => new("save failed", inner);

    [Fact]
    public void IsUniqueViolation_SameIndex_ReturnsTrue()
    {
        var ex = Wrap(Pg(PostgresErrorCodes.UniqueViolation, AppDbContext.InventoryNumberIndex));

        Assert.True(ex.IsUniqueViolation(AppDbContext.InventoryNumberIndex));
    }

    [Fact]
    public void IsUniqueViolation_OtherIndex_ReturnsFalse()
    {
        var ex = Wrap(Pg(PostgresErrorCodes.UniqueViolation, AppDbContext.TypeNameIndex));

        Assert.False(ex.IsUniqueViolation(AppDbContext.InventoryNumberIndex));
    }

    [Fact]
    public void IsUniqueViolation_OtherSqlState_ReturnsFalse()
    {
        var ex = Wrap(Pg(PostgresErrorCodes.CheckViolation, AppDbContext.InventoryNumberIndex));

        Assert.False(ex.IsUniqueViolation(AppDbContext.InventoryNumberIndex));
    }

    [Fact]
    public void IsUniqueViolation_NotPostgresError_ReturnsFalse()
    {
        Assert.False(Wrap(new InvalidOperationException()).IsUniqueViolation(AppDbContext.InventoryNumberIndex));
    }

    [Fact]
    public void IsForeignKeyViolation_DirectPostgresException_ReturnsTrue()
    {
        // ExecuteDelete выбрасывает ошибку PostgreSQL без обертки.
        Assert.True(Pg(PostgresErrorCodes.ForeignKeyViolation).IsForeignKeyViolation());
    }

    [Fact]
    public void IsForeignKeyViolation_WrappedPostgresException_ReturnsTrue()
    {
        Assert.True(Wrap(Pg(PostgresErrorCodes.ForeignKeyViolation)).IsForeignKeyViolation());
    }

    [Fact]
    public void IsForeignKeyViolation_OtherErrors_ReturnFalse()
    {
        Assert.False(Pg(PostgresErrorCodes.UniqueViolation).IsForeignKeyViolation());
        Assert.False(new InvalidOperationException().IsForeignKeyViolation());
    }

    [Fact]
    public void Conflict_Returns409WithCode()
    {
        var result = Assert.IsType<ProblemHttpResult>(Problems.Conflict("Заголовок", "Описание", "duplicate"));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("Заголовок", result.ProblemDetails.Title);
        Assert.Equal("Описание", result.ProblemDetails.Detail);
        Assert.Equal("duplicate", result.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void NotFound_Returns404()
    {
        var result = Assert.IsType<ProblemHttpResult>(Problems.NotFound("нет записи"));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("нет записи", result.ProblemDetails.Detail);
    }

    [Fact]
    public void BadRequest_Returns400()
    {
        var result = Assert.IsType<ProblemHttpResult>(Problems.BadRequest("неверный sort"));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }
}
