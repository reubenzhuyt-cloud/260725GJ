using Hotel.Runtime;
using NUnit.Framework;

namespace Hotel.Runtime.Tests
{
    public sealed class GameSettingsCodecTests
    {
        [Test]
        public void RoundTrip_PreservesAllSettings()
        {
            var data = new GameSettingsData
            {
                FullScreen = true,
                ResolutionWidth = 2560,
                ResolutionHeight = 1440,
                TargetFrameRate = 144,
                BgmVolume = 0.75f,
                SfxVolume = 0.25f
            };

            var json = GameSettingsCodec.ToJson(data);
            var restored = GameSettingsCodec.FromJson(json);

            Assert.That(restored.SchemaVersion, Is.EqualTo(GameSettingsData.CurrentSchemaVersion));
            Assert.That(restored.FullScreen, Is.True);
            Assert.That(restored.ResolutionWidth, Is.EqualTo(2560));
            Assert.That(restored.ResolutionHeight, Is.EqualTo(1440));
            Assert.That(restored.TargetFrameRate, Is.EqualTo(144));
            Assert.That(restored.BgmVolume, Is.EqualTo(0.75f));
            Assert.That(restored.SfxVolume, Is.EqualTo(0.25f));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{oops not json")]
        [TestCase("null")]
        public void FromJson_InvalidInput_ReturnsDefaults(string? json)
        {
            var data = GameSettingsCodec.FromJson(json);

            AssertDefault(data);
        }

        [TestCase("{\"SchemaVersion\":0}")]
        [TestCase("{\"SchemaVersion\":999}")]
        public void FromJson_OldOrUnknownSchema_ReturnsDefaults(string json)
        {
            var data = GameSettingsCodec.FromJson(json);

            AssertDefault(data);
        }

        [Test]
        public void FromJson_VolumeValues_AreClamped()
        {
            const string json = "{\"SchemaVersion\":1,\"BgmVolume\":1.7,\"SfxVolume\":-0.5}";

            var data = GameSettingsCodec.FromJson(json);

            Assert.That(data.BgmVolume, Is.EqualTo(1f));
            Assert.That(data.SfxVolume, Is.EqualTo(0f));
        }

        [TestCase(0, 1080)]
        [TestCase(-1280, -720)]
        public void FromJson_IllegalResolution_FallsBackToDefault(int width, int height)
        {
            var json = $"{{\"SchemaVersion\":1,\"ResolutionWidth\":{width},\"ResolutionHeight\":{height}}}";

            var data = GameSettingsCodec.FromJson(json);

            Assert.That(data.ResolutionWidth, Is.EqualTo(GameSettingsCodec.DefaultResolutionWidth));
            Assert.That(data.ResolutionHeight, Is.EqualTo(GameSettingsCodec.DefaultResolutionHeight));
        }

        [TestCase(-1)]
        [TestCase(45)]
        [TestCase(999)]
        public void FromJson_UnsupportedFrameRate_FallsBackToDefault(int frameRate)
        {
            var json = $"{{\"SchemaVersion\":1,\"TargetFrameRate\":{frameRate}}}";

            var data = GameSettingsCodec.FromJson(json);

            Assert.That(data.TargetFrameRate, Is.EqualTo(GameSettingsCodec.DefaultTargetFrameRate));
        }

        [TestCase(0)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        [TestCase(144)]
        [TestCase(165)]
        [TestCase(240)]
        public void FromJson_SupportedFrameRate_IsPreserved(int frameRate)
        {
            var json = $"{{\"SchemaVersion\":1,\"TargetFrameRate\":{frameRate}}}";

            var data = GameSettingsCodec.FromJson(json);

            Assert.That(data.TargetFrameRate, Is.EqualTo(frameRate));
        }

        [Test]
        public void FreshInstance_IsValidDefaultSettings()
        {
            var data = new GameSettingsData();

            Assert.That(data.SchemaVersion, Is.EqualTo(GameSettingsData.CurrentSchemaVersion));
            Assert.That(data.FullScreen, Is.False);
            Assert.That(data.ResolutionWidth, Is.EqualTo(GameSettingsCodec.DefaultResolutionWidth));
            Assert.That(data.ResolutionHeight, Is.EqualTo(GameSettingsCodec.DefaultResolutionHeight));
            Assert.That(data.TargetFrameRate, Is.EqualTo(GameSettingsCodec.DefaultTargetFrameRate));
            Assert.That(data.BgmVolume, Is.EqualTo(1f));
            Assert.That(data.SfxVolume, Is.EqualTo(1f));
        }

        private static void AssertDefault(GameSettingsData data)
        {
            Assert.That(data, Is.Not.Null);
            Assert.That(data.SchemaVersion, Is.EqualTo(GameSettingsData.CurrentSchemaVersion));
            Assert.That(data.FullScreen, Is.False);
            Assert.That(data.ResolutionWidth, Is.EqualTo(GameSettingsCodec.DefaultResolutionWidth));
            Assert.That(data.ResolutionHeight, Is.EqualTo(GameSettingsCodec.DefaultResolutionHeight));
            Assert.That(data.TargetFrameRate, Is.EqualTo(GameSettingsCodec.DefaultTargetFrameRate));
            Assert.That(data.BgmVolume, Is.EqualTo(1f));
            Assert.That(data.SfxVolume, Is.EqualTo(1f));
        }
    }
}
