FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Shared.Contracts/Shared.Contracts.csproj", "Shared.Contracts/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["OrderProcessingService/OrderProcessingService.csproj", "OrderProcessingService/"]
COPY ["Directory.Build.props", "."]
RUN dotnet restore "OrderProcessingService/OrderProcessingService.csproj"

COPY . .
RUN dotnet build "OrderProcessingService/OrderProcessingService.csproj" -c Release --no-restore
RUN dotnet publish "OrderProcessingService/OrderProcessingService.csproj" -c Release -o /app/publish --no-build

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderProcessingService.dll"]
