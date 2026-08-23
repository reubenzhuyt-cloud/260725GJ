using System;
using UnityEngine;

namespace Hotel.Audio
{
    public class UIEqualizerFilter : MonoBehaviour
    {
        private sealed class FilterCoefficients
        {
            public bool bypass;
            public float b0_low, b1_low, b2_low, a1_low, a2_low;
            public float b0_mid, b1_mid, b2_mid, a1_mid, a2_mid;
            public float b0_high, b1_high, b2_high, a1_high, a2_high;
        }

        private struct BiquadState
        {
            public float x1;
            public float x2;
            public float y1;
            public float y2;

            private const float DenormalThreshold = 1e-15f;

            public float Process(float inSample, float b0, float b1, float b2, float a1, float a2)
            {
                if (float.IsNaN(inSample) || float.IsInfinity(inSample))
                {
                    Reset();
                    return 0f;
                }

                float outSample = b0 * inSample + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                if (float.IsNaN(outSample) || float.IsInfinity(outSample))
                {
                    Reset();
                    return 0f;
                }

                x2 = (x1 > -DenormalThreshold && x1 < DenormalThreshold) ? 0f : x1;
                x1 = (inSample > -DenormalThreshold && inSample < DenormalThreshold) ? 0f : inSample;
                y2 = (y1 > -DenormalThreshold && y1 < DenormalThreshold) ? 0f : y1;
                y1 = (outSample > -DenormalThreshold && outSample < DenormalThreshold) ? 0f : outSample;

                return outSample;
            }

            public void Reset()
            {
                x1 = 0f;
                x2 = 0f;
                y1 = 0f;
                y2 = 0f;
            }
        }

        private const int MaxChannels = 8;
        private const float LowCutoff = 250f;
        private const float MidCenter = 1000f;
        private const float MidQ = 0.707f;
        private const float HighCutoff = 4000f;

        private readonly BiquadState[] lowStates = new BiquadState[MaxChannels];
        private readonly BiquadState[] midStates = new BiquadState[MaxChannels];
        private readonly BiquadState[] highStates = new BiquadState[MaxChannels];

        private volatile FilterCoefficients activeCoefficients;

        private float currentLowDb = 0f;
        private float currentMidDb = 0f;
        private float currentHighDb = 0f;
        private int lastSampleRate = 0;

        private void Awake()
        {
            RecomputeCoefficients();
        }

        private void OnEnable()
        {
            ResetStates();
        }

        public void SetGains(float lowDb, float midDb, float highDb)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0)
                sampleRate = 48000;

            if (Mathf.Approximately(currentLowDb, lowDb) &&
                Mathf.Approximately(currentMidDb, midDb) &&
                Mathf.Approximately(currentHighDb, highDb) &&
                lastSampleRate == sampleRate)
            {
                return;
            }

            currentLowDb = lowDb;
            currentMidDb = midDb;
            currentHighDb = highDb;
            lastSampleRate = sampleRate;

            RecomputeCoefficients();
        }

        private void RecomputeCoefficients()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0)
                sampleRate = 48000;

            if (Mathf.Abs(currentLowDb) < 0.001f &&
                Mathf.Abs(currentMidDb) < 0.001f &&
                Mathf.Abs(currentHighDb) < 0.001f)
            {
                FilterCoefficients bypassCoeffs = new FilterCoefficients { bypass = true };
                activeCoefficients = bypassCoeffs;
                return;
            }

            FilterCoefficients coeffs = new FilterCoefficients { bypass = false };

            CalculateLowShelf(LowCutoff, currentLowDb, sampleRate,
                out coeffs.b0_low, out coeffs.b1_low, out coeffs.b2_low,
                out coeffs.a1_low, out coeffs.a2_low);

            CalculatePeaking(MidCenter, MidQ, currentMidDb, sampleRate,
                out coeffs.b0_mid, out coeffs.b1_mid, out coeffs.b2_mid,
                out coeffs.a1_mid, out coeffs.a2_mid);

            CalculateHighShelf(HighCutoff, currentHighDb, sampleRate,
                out coeffs.b0_high, out coeffs.b1_high, out coeffs.b2_high,
                out coeffs.a1_high, out coeffs.a2_high);

            activeCoefficients = coeffs;
        }

        private void ResetStates()
        {
            for (int i = 0; i < MaxChannels; i++)
            {
                lowStates[i].Reset();
                midStates[i].Reset();
                highStates[i].Reset();
            }
        }

        private static void CalculateLowShelf(float freq, float gainDb, float sampleRate,
            out float b0, out float b1, out float b2, out float a1, out float a2)
        {
            freq = Mathf.Clamp(freq, 10f, sampleRate * 0.49f);
            float a = Mathf.Pow(10f, gainDb / 40f);
            float w0 = 2f * Mathf.PI * freq / sampleRate;
            float cosW = Mathf.Cos(w0);
            float sinW = Mathf.Sin(w0);
            float alpha = sinW / 2f * 1.41421356f;
            float twoSqrtAAlpha = 2f * Mathf.Sqrt(a) * alpha;

            float a0 = (a + 1f) + (a - 1f) * cosW + twoSqrtAAlpha;
            a1 = -2f * ((a - 1f) + (a + 1f) * cosW) / a0;
            a2 = ((a + 1f) + (a - 1f) * cosW - twoSqrtAAlpha) / a0;
            b0 = (a * ((a + 1f) - (a - 1f) * cosW + twoSqrtAAlpha)) / a0;
            b1 = (2f * a * ((a - 1f) - (a + 1f) * cosW)) / a0;
            b2 = (a * ((a + 1f) - (a - 1f) * cosW - twoSqrtAAlpha)) / a0;
        }

        private static void CalculatePeaking(float freq, float q, float gainDb, float sampleRate,
            out float b0, out float b1, out float b2, out float a1, out float a2)
        {
            freq = Mathf.Clamp(freq, 10f, sampleRate * 0.49f);
            float a = Mathf.Pow(10f, gainDb / 40f);
            float w0 = 2f * Mathf.PI * freq / sampleRate;
            float cosW = Mathf.Cos(w0);
            float sinW = Mathf.Sin(w0);
            float alpha = sinW / (2f * q);

            float a0 = 1f + alpha / a;
            a1 = (-2f * cosW) / a0;
            a2 = (1f - alpha / a) / a0;
            b0 = (1f + alpha * a) / a0;
            b1 = (-2f * cosW) / a0;
            b2 = (1f - alpha * a) / a0;
        }

        private static void CalculateHighShelf(float freq, float gainDb, float sampleRate,
            out float b0, out float b1, out float b2, out float a1, out float a2)
        {
            freq = Mathf.Clamp(freq, 10f, sampleRate * 0.49f);
            float a = Mathf.Pow(10f, gainDb / 40f);
            float w0 = 2f * Mathf.PI * freq / sampleRate;
            float cosW = Mathf.Cos(w0);
            float sinW = Mathf.Sin(w0);
            float alpha = sinW / 2f * 1.41421356f;
            float twoSqrtAAlpha = 2f * Mathf.Sqrt(a) * alpha;

            float a0 = (a + 1f) - (a - 1f) * cosW + twoSqrtAAlpha;
            a1 = (2f * ((a - 1f) - (a + 1f) * cosW)) / a0;
            a2 = ((a + 1f) - (a - 1f) * cosW - twoSqrtAAlpha) / a0;
            b0 = (a * ((a + 1f) + (a - 1f) * cosW + twoSqrtAAlpha)) / a0;
            b1 = (-2f * a * ((a - 1f) + (a + 1f) * cosW)) / a0;
            b2 = (a * ((a + 1f) + (a - 1f) * cosW - twoSqrtAAlpha)) / a0;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            FilterCoefficients coeffs = activeCoefficients;
            if (coeffs == null || coeffs.bypass || channels <= 0 || channels > MaxChannels)
                return;

            float b0L = coeffs.b0_low, b1L = coeffs.b1_low, b2L = coeffs.b2_low, a1L = coeffs.a1_low, a2L = coeffs.a2_low;
            float b0M = coeffs.b0_mid, b1M = coeffs.b1_mid, b2M = coeffs.b2_mid, a1M = coeffs.a1_mid, a2M = coeffs.a2_mid;
            float b0H = coeffs.b0_high, b1H = coeffs.b1_high, b2H = coeffs.b2_high, a1H = coeffs.a1_high, a2H = coeffs.a2_high;

            int length = data.Length;
            for (int i = 0; i < length; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    float s = data[i + ch];
                    s = lowStates[ch].Process(s, b0L, b1L, b2L, a1L, a2L);
                    s = midStates[ch].Process(s, b0M, b1M, b2M, a1M, a2M);
                    s = highStates[ch].Process(s, b0H, b1H, b2H, a1H, a2H);
                    if (s > 1f)
                        s = 1f;
                    else if (s < -1f)
                        s = -1f;
                    data[i + ch] = s;
                }
            }
        }
    }
}
