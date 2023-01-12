#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Nerves.API.Server/Nerves.API.Server.csproj", "Nerves.API.Server/"]
RUN dotnet restore "Nerves.API.Server/Nerves.API.Server.csproj"
COPY . .
WORKDIR "/src/Nerves.API.Server"
RUN dotnet build "Nerves.API.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nerves.API.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nerves.API.Server.dll"]