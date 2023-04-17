FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ExpiredKeyRemover/ExpiredKeyRemover.csproj", "ExpiredKeyRemover/"]
RUN dotnet restore "ExpiredKeyRemover/ExpiredKeyRemover.csproj"
COPY . .
WORKDIR "/src/ExpiredKeyRemover"
RUN dotnet build "ExpiredKeyRemover.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpiredKeyRemover.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpiredKeyRemover.dll"]
