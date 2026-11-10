# One Dockerfile for every Nerves service.
#   docker build -f deploy/docker/Dockerfile \\
#     --build-arg PROJECT=src/services/Nerves.Identity/Nerves.Identity.csproj \\
#     --build-arg ENTRY_DLL=Nerves.Identity.dll .
ARG DOTNET_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY . .
ARG PROJECT
RUN dotnet publish "$PROJECT" -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
RUN apt-get update -o Acquire::Retries=5 -o Acquire::http::Timeout=30 \
    && apt-get install -y --no-install-recommends \
        -o Acquire::Retries=5 -o Acquire::http::Timeout=30 \
        libgssapi-krb5-2 libldap2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
ARG ENTRY_DLL
ENV ENTRY_DLL=${ENTRY_DLL}
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT exec dotnet "$ENTRY_DLL"
