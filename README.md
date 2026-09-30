# Music Trim

A small client-side Vintage Story mod (1.22+) that lowers the volume of base-game music, so quieter music tracks added by a mod don't sound out of place next to it.

## How it works

Every 250 ms the mod checks `ICoreClientAPI.CurrentMusicTrack`:

- If a base-game `SurfaceMusicTrack` is playing, it sets that track's volume to `original × TrimFactor`.
- If the playing track's name starts with a prefix listed in `KeepFullVolumePrefixes`, it leaves the track alone.
- When a track ends, the game disposes its sound. The next track starts at normal volume and is trimmed again on the following tick if needed.

The volume is set from the original value cached when the track started, so repeated ticks don't compound. The mod re-applies it on every tick because the game resets music volume when the player moves the music slider.

## Limitations

- Only surface music (`SurfaceMusicTrack`) is trimmed. Cave, rain and event music store their sounds differently and play at normal volume.
- The mod reads the public `Sound` field of a base-game class. If a game update or another mod replaces the music engine, the cast returns null and the mod does nothing.

## Config

The config file is created at `ModConfig/MusicTrim.json` on first launch. After that, the mod never rewrites it. If the file has a JSON error, the mod uses the defaults for that session, shows the error in chat and leaves the file unchanged.

```json
{
  "TrimFactor": 0.5,
  "KeepFullVolumePrefixes": [ "music/antique_harmony/", "music/ah_ambient/" ]
}
```

Tracks whose name starts with any entry in `KeepFullVolumePrefixes` keep their full volume. The defaults cover Antique Harmony, a content-only mod that places its songs in the `game` domain. The game names those tracks by path alone, for example `music/antique_harmony/dawn.ogg`. For a music mod that uses its own domain, add `"modid:"`, which matches tracks named like `modid:music/song.ogg`.

## Build

1. Install the .NET 10 SDK.
2. Set the `VINTAGE_STORY` environment variable to your game install folder (the one containing `VintagestoryAPI.dll`). If it isn't set, the build looks in `%APPDATA%\Vintagestory`.
3. Run `dotnet build -c Release`.
4. Zip `bin/Release/MusicTrim.dll` and `bin/Release/modinfo.json` together, with both files at the root of the zip, and put the zip in your `Mods` folder.
