# Build stage: publish the log server app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/EasySave.LogServer/EasySave.LogServer.csproj", "EasySave.LogServer/"]
COPY ["src/EasyLog/EasyLog.csproj", "EasyLog/"]
COPY ["src/EasySave.Core/EasySave.Core.csproj", "EasySave.Core/"]

RUN dotnet restore "EasySave.LogServer/EasySave.LogServer.csproj"
COPY src/EasySave.LogServer/ EasySave.LogServer/
COPY src/EasyLog/ EasyLog/
COPY src/EasySave.Core/ EasySave.Core/

WORKDIR /src/EasySave.LogServer
RUN dotnet publish "EasySave.LogServer.csproj" -c Release -o /app/publish --no-restore

# Runtime stage: run the service
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Default: listen on 9050, store logs in /logs (override with env)
ENV LOG_SERVER_PORT=9050
ENV LOG_DIR=/logs

EXPOSE 9050

ENTRYPOINT ["dotnet", "EasySave.LogServer.dll"]
