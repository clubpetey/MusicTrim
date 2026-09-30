using System;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace MusicTrim;

public class MusicTrimConfig
{
    /// <summary>Volume multiplier applied to music tracks that are not on the keep list (0.5 = half).</summary>
    public float TrimFactor = 0.5f;

    /// <summary>Tracks whose name starts with any of these are left at full volume. Names look like
    /// "music/antique_harmony/dawn.ogg" (files in the game domain) or "mymod:music/song.ogg".</summary>
    public string[] KeepFullVolumePrefixes = { "music/antique_harmony/", "music/ah_ambient/", "music/antiquecylinders" };
}

/// <summary>
/// Lowers the volume of base-game surface music so quieter mod-added tracks sit at a similar level.
/// Only one music track plays at a time; when it is one of ours it is left untouched.
/// </summary>
public class MusicTrimModSystem : ModSystem
{
    private ICoreClientAPI _api = null!;
    private MusicTrimConfig _config = null!;
    private long _tickId = -1;

    // The sound we are currently trimming, and its volume before we touched it.
    private ILoadedSound? _trimmed;
    private float _trimmedOrig;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        _api = api;
        _config = LoadConfig(api);
        _tickId = api.Event.RegisterGameTickListener(OnTick, 250);
    }

    private void OnTick(float dt)
    {
        try
        {
            // Cave, rain and event tracks keep their sounds elsewhere, so only surface music is handled.
            var track = _api.CurrentMusicTrack as SurfaceMusicTrack;
            var sound = track?.Sound;

            if (sound == null || sound.IsDisposed || !track!.IsActive || IsKept(track.Name))
            {
                _trimmed = null;
                return;
            }

            if (!ReferenceEquals(sound, _trimmed))
            {
                _trimmed = sound;
                _trimmedOrig = sound.Params.Volume;
            }

            // Set from the cached original every tick (never from the current volume, which would
            // compound). Re-applying also undoes the reset vanilla does when the music slider moves.
            sound.SetVolume(_trimmedOrig * _config.TrimFactor);
        }
        catch (Exception e)
        {
            _api.Logger.Warning("[MusicTrim] tick failed: " + e.Message);
        }
    }

    private bool IsKept(string trackName)
    {
        foreach (var prefix in _config.KeepFullVolumePrefixes)
        {
            if (trackName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static MusicTrimConfig LoadConfig(ICoreClientAPI api)
    {
        string path = Path.Combine(api.GetOrCreateDataPath("ModConfig"), "MusicTrim.json");

        // Only create the file when it is missing; never overwrite a file the user has edited.
        if (!File.Exists(path))
        {
            var defaults = new MusicTrimConfig();
            try { File.WriteAllText(path, JsonConvert.SerializeObject(defaults, Formatting.Indented)); }
            catch { /* read-only data dir: defaults still apply */ }
            return defaults;
        }

        MusicTrimConfig? config = null;
        try
        {
            config = JsonConvert.DeserializeObject<MusicTrimConfig>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            string msg = $"[MusicTrim] {path} is not valid JSON, using defaults for this session (file left unchanged): {e.Message}";
            api.Logger.Warning(msg);
            api.Event.LevelFinalize += () => api.ShowChatMessage(msg);
        }

        config ??= new MusicTrimConfig();
        if (config.TrimFactor < 0f || config.TrimFactor > 1f)
        {
            api.Logger.Warning($"[MusicTrim] TrimFactor {config.TrimFactor} is outside 0..1 (0.5 = half volume); clamping.");
            config.TrimFactor = Math.Clamp(config.TrimFactor, 0f, 1f);
        }
        config.KeepFullVolumePrefixes ??= Array.Empty<string>();
        return config;
    }

    public override void Dispose()
    {
        if (_api == null) return;
        if (_tickId >= 0) _api.Event.UnregisterGameTickListener(_tickId);
        _tickId = -1;

        if (_trimmed != null && !_trimmed.IsDisposed) _trimmed.SetVolume(_trimmedOrig);
        _trimmed = null;
    }
}
