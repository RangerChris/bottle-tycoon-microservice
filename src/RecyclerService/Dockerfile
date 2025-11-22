FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/RecyclerService/RecyclerService.csproj ./
# Ensure the referenced Shared project is available at the path expected by the project reference
COPY src/Shared/Shared.csproj ../Shared/Shared.csproj
RUN dotnet restore "RecyclerService.csproj"

COPY src/RecyclerService/. .
# Copy Shared sources into the container so the project reference can be resolved during publish
COPY src/Shared/. ../Shared/
RUN dotnet publish "RecyclerService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "RecyclerService.dll"]