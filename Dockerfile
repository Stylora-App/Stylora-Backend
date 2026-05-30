FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore layer — copied separately so it caches unless a .csproj changes
COPY Stylora.sln .
COPY Stylora.Domain/Stylora.Domain.csproj                         Stylora.Domain/
COPY Stylora.Application/Stylora.Application.csproj               Stylora.Application/
COPY Stylora.Infrastructure/Stylora.Infrastructure.csproj         Stylora.Infrastructure/
COPY Stylora.API/Stylora.API.csproj                               Stylora.API/
COPY Stylora.Application.Tests/Stylora.Application.Tests.csproj   Stylora.Application.Tests/
RUN dotnet restore Stylora.sln

COPY . .
RUN dotnet tool install --global NSwag.ConsoleCore --version 14.2.0
ENV PATH="$PATH:/root/.dotnet/tools"
RUN cd Stylora.Infrastructure && nswag run nswag/clip.nswag && nswag run nswag/gemma.nswag
RUN dotnet build Stylora.sln -c Release --no-restore

# ── Test ─────────────────────────────────────────────────────────────────────
FROM build AS test
RUN dotnet test Stylora.Application.Tests/Stylora.Application.Tests.csproj \
      -c Release --no-build --logger "console;verbosity=normal"

# ── Publish ───────────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish Stylora.API/Stylora.API.csproj \
      -c Release -o /app/publish --no-build

# ── Runtime ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENV ClothingValidation__SeedDirectoryPath=/data/clothing-validation
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Stylora.API.dll"]
