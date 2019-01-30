<h1 align="center">Jellyfin Anime Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.media">Jellyfin Project</a></h3>

<p align="center">
Jellfin anime plugin is a plugin built with .NET
</p>

## Build Process
1. Clone or download this repository
2. Ensure you have .NET Core SDK setup and installed
3. Build plugin with following command.
```sh
dotnet publish --self-contained --runtime <os-architecture-here> --configuration Release --output bin
```
4. Place the resulting .dll file in a folder called ```plugins/``` under  the program data directory or inside the portable install directory
