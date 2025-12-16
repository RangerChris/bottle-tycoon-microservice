# Use the official .NET 10 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/GameService/GameService.csproj ./
RUN dotnet restore "GameService.csproj"

COPY src/GameService/. .
RUN dotnet build "GameService.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "GameService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache icu-libs krb5-libs
COPY --from=publish /app/publish .

# Expose the port the app runs on
EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "GameService.dll"]