FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Shared.Contracts/Shared.Contracts.csproj", "Shared.Contracts/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["WebhookService/WebhookService.csproj", "WebhookService/"]
COPY ["Directory.Build.props", "."]
RUN dotnet restore "WebhookService/WebhookService.csproj"

COPY . .
RUN dotnet build "WebhookService/WebhookService.csproj" -c Release --no-restore
RUN dotnet publish "WebhookService/WebhookService.csproj" -c Release -o /app/publish --no-build

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WebhookService.dll"]
