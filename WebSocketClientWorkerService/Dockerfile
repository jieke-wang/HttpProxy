#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["WebSocketClientWorkerService/WebSocketClientWorkerService.csproj", "WebSocketClientWorkerService/"]
RUN dotnet restore "WebSocketClientWorkerService/WebSocketClientWorkerService.csproj"
COPY . .
WORKDIR "/src/WebSocketClientWorkerService"
RUN dotnet build "WebSocketClientWorkerService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebSocketClientWorkerService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebSocketClientWorkerService.dll"]