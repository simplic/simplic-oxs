# syntax=docker/dockerfile:1.2

ARG nuget_user

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base

ARG nuget_user

WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

ARG nuget_user

COPY . .

RUN --mount=type=secret,id=pat,dst=/pat dotnet nuget add source "https://pkgs.dev.azure.com/simplic-cloud/_packaging/simplic-cloud/nuget/v3/index.json" --name simplic --username "$nuget_user" --password "`cat /pat`" --store-password-in-clear-text

RUN dotnet restore

RUN dotnet build -c Release -o /app --no-restore

FROM build AS publish
RUN dotnet publish -c Release -o /app --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Simplic.OxS.Auth.Server.dll"]
