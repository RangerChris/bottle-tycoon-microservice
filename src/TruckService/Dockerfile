# Use the official .NET 9 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/TruckService/TruckService.csproj ./
# Ensure the referenced Shared project is available at the path expected by the project reference
COPY src/Shared/Shared.csproj ../Shared/Shared.csproj
RUN dotnet restore "TruckService.csproj"

COPY src/TruckService/. ./
# Copy Shared sources into the container so the project reference can be resolved during build/publish
COPY src/Shared/. ../Shared/
RUN dotnet build "TruckService.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TruckService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose the port the app runs on
EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "TruckService.dll"]