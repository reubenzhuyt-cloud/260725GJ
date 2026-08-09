using System;
using UnityEngine;

namespace Hotel.Runtime
{
    [Serializable]
    public sealed class GameSettingsData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public bool FullScreen;
        public int ResolutionWidth = GameSettingsCodec.DefaultResolutionWidth;
        public int ResolutionHeight = GameSettingsCodec.DefaultResolutionHeight;
        public int TargetFrameRate = GameSettingsCodec.DefaultTargetFrameRate;
        public float BgmVolume = 1f;
        public float SfxVolume = 1f;
    }

    public static class GameSettingsCodec
    {
        public const int DefaultResolutionWidth = 1920;
        public const int DefaultResolutionHeight = 1080;
        public const int DefaultTargetFrameRate = 0;

        public static readonly int[] SupportedFrameRates = { 30, 60, 120, 144, 165, 240, 0 };

        public static string ToJson(GameSettingsData data, bool prettyPrint = true)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return JsonUtility.ToJson(Normalize(data), prettyPrint);
        }

        public static GameSettingsData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Default();

            GameSettingsData data;
            try
            {
                data = JsonUtility.FromJson<GameSettingsData>(json);
            }
            catch (ArgumentException)
            {
                return Default();
            }

            if (data == null || data.SchemaVersion != GameSettingsData.CurrentSchemaVersion)
                return Default();

            return Normalize(data);
        }

        public static GameSettingsData Default()
        {
            return new GameSettingsData();
        }

        public static GameSettingsData Normalize(GameSettingsData data)
        {
            if (data == null) return Default();

            data.SchemaVersion = GameSettingsData.CurrentSchemaVersion;
            data.BgmVolume = Mathf.Clamp01(data.BgmVolume);
            data.SfxVolume = Mathf.Clamp01(data.SfxVolume);

            if (data.ResolutionWidth <= 0 || data.ResolutionHeight <= 0)
            {
                data.ResolutionWidth = DefaultResolutionWidth;
                data.ResolutionHeight = DefaultResolutionHeight;
            }

            if (!IsSupportedFrameRate(data.TargetFrameRate))
                data.TargetFrameRate = DefaultTargetFrameRate;

            return data;
        }

        public static bool IsSupportedFrameRate(int frameRate)
        {
            if (frameRate == 0) return true;
            for (int i = 0; i < SupportedFrameRates.Length; i++)
            {
                if (SupportedFrameRates[i] == frameRate) return true;
            }
            return false;
        }
    }
}
