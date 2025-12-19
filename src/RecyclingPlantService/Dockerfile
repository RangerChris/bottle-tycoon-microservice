# Use the official .NET 10 SDK image to build the application
FROM dhi.io/dotnet:10-sdk AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/RecyclingPlantService/RecyclingPlantService.csproj ./
RUN dotnet restore "RecyclingPlantService.csproj"

COPY src/RecyclingPlantService/. .
RUN dotnet build "RecyclingPlantService.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "RecyclingPlantService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the official ASP.NET Core runtime image
FROM dhi.io/aspnetcore:10-alpine3.22 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose the port the app runs on
EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "RecyclingPlantService.dll"]