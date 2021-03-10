**ARCHIVE NOTE**: Deprecated in favour of jellyfin-plugin-anidb, jellyfin-plugin-anisearch, jellyfin-plugin-anilist, and jellyfin-plugin-kitsu

<h1 align="center">Jellyfin Anime Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.media">Jellyfin Project</a></h3>

<p align="center">
This plugin is built with .NET Core to download metadata for anime.
</p>

## Archived

This plugin has been retired and split apart into their respective metadata plugins: [anilist](https://github.com/jellyfin/jellyfin-plugin-anilist), [anidb](https://github.com/jellyfin/jellyfin-plugin-anidb), [anisearch](https://github.com/jellyfin/jellyfin-plugin-anisearch), and [kitsu](https://github.com/jellyfin/jellyfin-plugin-kitsu).

## Build Process

1. Clone or download this repository

2. Ensure you have .NET Core SDK setup and installed

3. Build plugin with following command.

```sh
dotnet publish --configuration Release --output bin
```
4. Place the resulting file in the `plugins` folder under the program data directory or inside the portable install directory
