FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ComputerEquipment.Api/ComputerEquipment.Api.csproj src/ComputerEquipment.Api/
RUN dotnet restore src/ComputerEquipment.Api/ComputerEquipment.Api.csproj

COPY src/ src/
RUN dotnet publish src/ComputerEquipment.Api/ComputerEquipment.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ComputerEquipment.Api.dll"]
