# ---------- Сборка: восстановление пакетов, тесты, публикация ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Пропустить тесты: docker compose build --build-arg SKIP_TESTS=true
ARG SKIP_TESTS=false

# Сначала только файлы проектов: слой с пакетами кешируется и не пересобирается при правках кода.
# Restore тестового проекта восстанавливает и API, на который он ссылается.
COPY src/ComputerEquipment.Api/ComputerEquipment.Api.csproj src/ComputerEquipment.Api/
COPY tests/ComputerEquipment.Api.Tests/ComputerEquipment.Api.Tests.csproj tests/ComputerEquipment.Api.Tests/
RUN dotnet restore tests/ComputerEquipment.Api.Tests/ComputerEquipment.Api.Tests.csproj

COPY src/ src/
COPY tests/ tests/

# Юнит-тесты перед публикацией. Если хоть один тест упадет, сборка образа остановится
# и сломанная версия не попадет в контейнер. База данных тестам не нужна.
RUN if [ "$SKIP_TESTS" = "true" ]; then \
        echo "Тесты пропущены (SKIP_TESTS=true)"; \
    else \
        dotnet test tests/ComputerEquipment.Api.Tests/ComputerEquipment.Api.Tests.csproj \
            -c Release --no-restore --logger "console;verbosity=normal"; \
    fi

RUN dotnet publish src/ComputerEquipment.Api/ComputerEquipment.Api.csproj -c Release -o /app --no-restore

# ---------- Запуск: только среда выполнения ASP.NET Core, без SDK и тестов ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ComputerEquipment.Api.dll"]
