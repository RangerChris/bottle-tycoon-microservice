FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/HeadquartersService/HeadquartersService.csproj ./
RUN dotnet restore "HeadquartersService.csproj"

COPY src/HeadquartersService/. .
RUN dotnet build "HeadquartersService.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "HeadquartersService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache icu-libs krb5-libs
COPY --from=publish /app/publish .

# Expose the port the app runs on
EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "HeadquartersService.dll"]