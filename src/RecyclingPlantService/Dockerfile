FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/RecyclingPlantService/RecyclingPlantService.csproj ./RecyclingPlantService/
RUN dotnet restore "RecyclingPlantService/RecyclingPlantService.csproj"

COPY src/RecyclingPlantService/. ./RecyclingPlantService/
RUN dotnet publish "RecyclingPlantService/RecyclingPlantService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache icu-libs krb5-libs
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "RecyclingPlantService.dll"]