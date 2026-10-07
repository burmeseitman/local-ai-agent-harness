FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files
COPY SecureAiHarness.sln ./
COPY src/Harness.Models/Harness.Models.csproj src/Harness.Models/
COPY src/Harness.Core/Harness.Core.csproj src/Harness.Core/
COPY src/Harness.Tools/Harness.Tools.csproj src/Harness.Tools/
COPY src/Harness.Memory/Harness.Memory.csproj src/Harness.Memory/
COPY src/Harness.Observability/Harness.Observability.csproj src/Harness.Observability/
COPY src/Harness.Infrastructure/Harness.Infrastructure.csproj src/Harness.Infrastructure/
COPY src/Harness.Api/Harness.Api.csproj src/Harness.Api/
COPY tests/Harness.Core.Tests/Harness.Core.Tests.csproj tests/Harness.Core.Tests/
COPY tests/Harness.Api.Tests/Harness.Api.Tests.csproj tests/Harness.Api.Tests/
COPY tests/Harness.Tools.Tests/Harness.Tools.Tests.csproj tests/Harness.Tools.Tests/

# In Docker build, target net8.0
RUN sed -i 's/net10.0/net8.0/g' src/*/*.csproj tests/*/*.csproj || true

RUN dotnet restore SecureAiHarness.sln

# Copy all source code
COPY . .
RUN sed -i 's/net10.0/net8.0/g' src/*/*.csproj tests/*/*.csproj || true

# Publish API
RUN dotnet publish src/Harness.Api/Harness.Api.csproj -c Release -o /app/publish

# Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Harness.Api.dll"]
