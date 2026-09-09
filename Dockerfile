FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY SlowRoad/SlowRoad.csproj SlowRoad/
RUN dotnet restore SlowRoad/SlowRoad.csproj

COPY . .
RUN dotnet publish SlowRoad/SlowRoad.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SlowRoad.dll"]