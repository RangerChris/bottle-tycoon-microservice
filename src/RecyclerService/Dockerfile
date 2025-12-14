FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/RecyclerService/RecyclerService.csproj ./
RUN dotnet restore "RecyclerService.csproj"

COPY src/RecyclerService/. .
RUN dotnet publish "RecyclerService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "RecyclerService.dll"]