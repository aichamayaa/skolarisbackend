# Étape 1 : compilation avec le SDK .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SystemePlacement.Web/SystemePlacement.Web.csproj SystemePlacement.Web/
RUN dotnet restore SystemePlacement.Web/SystemePlacement.Web.csproj
COPY SystemePlacement.Web/ SystemePlacement.Web/
RUN dotnet publish SystemePlacement.Web/SystemePlacement.Web.csproj -c Release -o /out

# Étape 2 : exécution avec le runtime ASP.NET 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
# Le port est fourni par l'hébergeur via PORT (8080 par défaut)
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet SystemePlacement.Web.dll"]
