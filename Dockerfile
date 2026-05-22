# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=10.0

# --- Build stage --------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# BuildKit's COPY parser treats `'` as a quote character even inside the
# JSON-array form, so we cannot do the usual csproj-first restore trick
# (the T'au directory's apostrophe trips the parser). Copy the full
# context up front and restore in one shot. The .dockerignore keeps
# bin/, obj/, .git/ etc. out, so the context stays small.
COPY . .
RUN dotnet restore Warhammer.slnx
RUN dotnet publish "T'au/Warhammer.csproj" \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# --- Runtime stage ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Assembly name is overridden in the csproj to "Warhammer_Helper".
ENTRYPOINT ["dotnet", "Warhammer_Helper.dll"]
