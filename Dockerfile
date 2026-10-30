
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DeskPoint.csproj", "./"]
RUN dotnet restore "DeskPoint.csproj"

COPY . .
RUN dotnet publish "DeskPoint.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000

ENTRYPOINT ["sh", "-c", "dotnet DeskPoint.dll --urls http://0.0.0.0:${PORT:-10000}"]
