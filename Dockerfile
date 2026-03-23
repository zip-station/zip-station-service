FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ZipStation.Models/ZipStation.Models.csproj ZipStation.Models/
COPY ZipStation.Business/ZipStation.Business.csproj ZipStation.Business/
COPY ZipStation.Mapping/ZipStation.Mapping.csproj ZipStation.Mapping/
COPY ZipStation.Api/ZipStation.Api.csproj ZipStation.Api/
RUN dotnet restore ZipStation.Api/ZipStation.Api.csproj

COPY . .
RUN dotnet publish ZipStation.Api/ZipStation.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://*:80
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 80
ENTRYPOINT ["dotnet", "ZipStation.Api.dll"]
