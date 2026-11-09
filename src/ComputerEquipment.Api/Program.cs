using ComputerEquipment.Api.Data;
using ComputerEquipment.Api.Endpoints;
using ComputerEquipment.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

// Точка входа: настройка сервисов, подготовка БД, раздача клиента и регистрация Minimal API.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Настройки модуля из секции "Equipment"; некорректное значение остановит приложение при старте.
builder.Services.AddOptions<EquipmentOptions>()
    .BindConfiguration(EquipmentOptions.Section)
    .Validate(o => o.MaxRecords > 0, "Equipment:MaxRecords должен быть больше 0")
    .ValidateOnStart();

// Ошибки и необработанные исключения возвращаются в формате ProblemDetails.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Миграции и начальные данные — до приема запросов.
await DbInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);

// Тонкий клиент (DevExtreme) раздается тем же сервером из wwwroot — один запуск, без CORS.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapCatalogEndpoints();
app.MapEquipmentEndpoints();

app.Run();
