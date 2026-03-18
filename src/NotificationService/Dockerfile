FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Shared.Contracts/Shared.Contracts.csproj", "Shared.Contracts/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["NotificationService/NotificationService.csproj", "NotificationService/"]
COPY ["Directory.Build.props", "."]
RUN dotnet restore "NotificationService/NotificationService.csproj"

COPY . .
RUN dotnet build "NotificationService/NotificationService.csproj" -c Release --no-restore
RUN dotnet publish "NotificationService/NotificationService.csproj" -c Release -o /app/publish --no-build

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.dll"]
