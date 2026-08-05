# syntax=docker/dockerfile:1
#
# One shared, parameterized Dockerfile for every SeeSight .NET service — per
# SolutionStructure.md §1. Build context is the repo root (not this folder),
# since every service pulls in project references across src/Shared/** and its
# own src/Services/<X>/** — e.g.:
#   docker build -f docker/Dockerfile --build-arg PROJECT_PATH=src/Services/Identity/SeeSight.Identity.Api --build-arg PROJECT_NAME=SeeSight.Identity.Api .

ARG PROJECT_PATH
ARG PROJECT_NAME

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG PROJECT_PATH
ARG PROJECT_NAME
WORKDIR /src

COPY . .
RUN dotnet restore "${PROJECT_PATH}/${PROJECT_NAME}.csproj"
RUN dotnet publish "${PROJECT_PATH}/${PROJECT_NAME}.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
ARG PROJECT_NAME
ENV PROJECT_NAME=${PROJECT_NAME}
ENV ASPNETCORE_URLS=http://+:8080

# The base runtime image has neither curl nor wget — installed here so each
# service's own Docker HEALTHCHECK (declared per-service in docker-compose.yml)
# has something to call /health/live and /health/ready with.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet ${PROJECT_NAME}.dll"]
