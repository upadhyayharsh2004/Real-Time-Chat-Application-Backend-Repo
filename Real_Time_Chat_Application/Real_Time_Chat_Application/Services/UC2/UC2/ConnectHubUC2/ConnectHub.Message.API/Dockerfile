FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ConnectHub.Message.API.csproj", "."]
RUN dotnet restore "ConnectHub.Message.API.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "ConnectHub.Message.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConnectHub.Message.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConnectHub.Message.API.dll"]
