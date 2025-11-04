# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ErrorAnalysisBackend.csproj ./
RUN dotnet restore "ErrorAnalysisBackend.csproj"

# Copy the rest and publish
COPY . .
RUN dotnet publish "ErrorAnalysisBackend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5294

EXPOSE 5294

ENTRYPOINT ["dotnet", "ErrorAnalysisBackend.dll"]
