FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файл проекта и скачиваем зависимости
COPY ["NatCoordinator.csproj", "./"]
RUN dotnet restore

# Копируем остальной код и собираем проект
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# Указываем Render порт по умолчанию для HTTP (для прохождения проверки доступности)
EXPOSE 10000
ENV PORT=10000

ENTRYPOINT ["dotnet", "NatCoordinator.dll"]