FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore TallyEnergyBilling.slnx
RUN dotnet publish src/TallyEnergyBilling.Api/TallyEnergyBilling.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8094
EXPOSE 8094
ENTRYPOINT ["dotnet", "TallyEnergyBilling.Api.dll"]
