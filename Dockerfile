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

# Create secrets dir and copy the Firebase key from your repo
RUN mkdir -p /app/Secrets
COPY --from=build /app/publish .
COPY Secrets/serviceAccountKey.json /app/Secrets/serviceAccountKey.json

# App configuration
ENV ASPNETCORE_ENVIRONMENT=Production
# Bind to the same port you use locally
ENV ASPNETCORE_URLS=http://+:5294
# Let Google SDKs find your key
ENV GOOGLE_APPLICATION_CREDENTIALS=/app/Secrets/serviceAccountKey.json

# Expose container port
EXPOSE 5294

ENTRYPOINT ["dotnet", "ErrorAnalysisBackend.dll"]
