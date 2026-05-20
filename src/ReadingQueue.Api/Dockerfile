FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["ReadingQueue.Api/ReadingQueue.Api.csproj",                       "ReadingQueue.Api/"]
COPY ["ReadingQueue.Application/ReadingQueue.Application.csproj",       "ReadingQueue.Application/"]
COPY ["ReadingQueue.Domain/ReadingQueue.Domain.csproj",                 "ReadingQueue.Domain/"]
COPY ["ReadingQueue.Infrastructure/ReadingQueue.Infrastructure.csproj", "ReadingQueue.Infrastructure/"]

RUN dotnet restore "ReadingQueue.Api/ReadingQueue.Api.csproj"

COPY . .
RUN dotnet publish "ReadingQueue.Api/ReadingQueue.Api.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReadingQueue.Api.dll"]
