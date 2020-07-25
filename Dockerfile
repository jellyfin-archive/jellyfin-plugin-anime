# syntax=docker/dockerfile:1.1-experimental
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS base

WORKDIR /src
ENV \
    BaseIntermediateOutputPath=/obj/

FROM base AS build
RUN --mount=target=. \
    --mount=type=cache,target=/root/.nuget \
    --mount=type=tmpfs,target=/obj \
    dotnet build --configuration Debug --output /out

FROM base AS release
RUN --mount=target=.,rw \
    --mount=type=cache,target=/root/.nuget \
    --mount=type=tmpfs,target=/obj \
    dotnet publish --configuration Release --output /out

FROM scratch AS final
COPY --from=release /out/Jellyfin.Plugin.* /
