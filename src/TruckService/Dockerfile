# Use the official .NET 10 SDK image to build the application
FROM dhi.io/dotnet:10-sdk AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY src/TruckService/TruckService.csproj ./
RUN dotnet restore "TruckService.csproj"

COPY src/TruckService/. ./
RUN dotnet build "TruckService.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TruckService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Get Kerberos libraries from Alpine
FROM alpine:3.22 AS krb5-libs
RUN apk add --no-cache krb5-libs
RUN mkdir -p /export/usr/lib && cp /usr/lib/libgssapi_krb5.so* /export/usr/lib/ && \
    cp /usr/lib/libkrb5.so* /export/usr/lib/ && \
    cp /usr/lib/libkrb5support.so* /export/usr/lib/ && \
    cp /usr/lib/libk5crypto.so* /export/usr/lib/ && \
    cp /usr/lib/libcom_err.so* /export/usr/lib/

# Use the official ASP.NET Core runtime image
FROM dhi.io/aspnetcore:10-alpine3.22 AS final
WORKDIR /app
COPY --from=krb5-libs /export/usr/lib/* /usr/lib/

COPY --from=publish /app/publish .

# Expose the port the app runs on
EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "TruckService.dll"]