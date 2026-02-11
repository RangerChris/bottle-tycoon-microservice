# Use the official .NET 10 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the solution file and restore dependencies
COPY ["bottle-tycoon-microservice.sln", "."]
COPY ["src/GameService/GameService.csproj", "src/GameService/"]
COPY ["src/GameService.Tests/GameService.Tests.csproj", "src/GameService.Tests/"]
RUN dotnet restore "src/GameService/GameService.csproj"

# Copy the source code and build
COPY ["src/GameService/", "src/GameService/"]
COPY ["src/GameService.Tests/", "src/GameService.Tests/"]
WORKDIR "/src/src/GameService"
RUN dotnet build "GameService.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "GameService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .


# Expose the port the app runs on
EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "GameService.dll"]