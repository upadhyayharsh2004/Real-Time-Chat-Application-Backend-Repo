FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ConnectHub.Media.API.csproj", "."]
RUN dotnet restore "ConnectHub.Media.API.csproj"
COPY . .
RUN dotnet build "ConnectHub.Media.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConnectHub.Media.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConnectHub.Media.API.dll"]
