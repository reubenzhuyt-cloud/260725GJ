using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class EventFontPrewarmer : MonoBehaviour
{
    [Header("Fonts")]
    [Tooltip("Static primary font (NotoSansSC-VF SDF). Glyphs it already contains are skipped.")]
    [SerializeField] private TMP_FontAsset primaryFont;

    [Tooltip("Dynamic fallback font to prewarm (NotoSansSC-VF SDF_FallBack). Must be Dynamic.")]
    [SerializeField] private TMP_FontAsset fallbackFont;

    [Header("Tuning")]
    [Tooltip("Max glyphs added per TryAddCharacters batch before yielding one frame.")]
    [SerializeField, Min(1)] private int batchSize = 64;

    private static readonly string[] FixedEventStrings =
    {
        "普通", "故事", "个人", "特殊", "·",
    };

    private static readonly HashSet<TMP_FontAsset> PrewarmedFonts = new HashSet<TMP_FontAsset>();

    private void Start()
    {
        StartCoroutine(PrewarmRoutine());
    }

    private IEnumerator PrewarmRoutine()
    {
        if (primaryFont == null || fallbackFont == null)
        {
            Debug.LogWarning("[EventFontPrewarmer] Missing font reference(s). Assign primaryFont and fallbackFont in the inspector; prewarm skipped.");
            yield break;
        }

        if (fallbackFont.atlasPopulationMode != AtlasPopulationMode.Dynamic)
        {
            Debug.LogWarning($"[EventFontPrewarmer] Target font '{fallbackFont.name}' is not dynamic (mode: {fallbackFont.atlasPopulationMode}); prewarm skipped.");
            yield break;
        }

        if (PrewarmedFonts.Contains(fallbackFont))
            yield break;

        var eventManager = GetComponent<EventManager>();
        if (eventManager == null || eventManager.allEvents == null || eventManager.allEvents.Count == 0)
        {
            Debug.LogWarning("[EventFontPrewarmer] No EventManager with allEvents found on this GameObject; prewarm skipped.");
            yield break;
        }

        HashSet<uint> covered = GetCharacterSet(primaryFont);
        HashSet<uint> inFallback = GetCharacterSet(fallbackFont);
        HashSet<uint> needed = new HashSet<uint>();

        for (int i = 0; i < eventManager.allEvents.Count; i++)
        {
            EventConfig config = eventManager.allEvents[i];
            if (config == null) continue;

            CollectNeeded(config.eventTitle, covered, inFallback, needed);
            CollectNeeded(config.eventDescription, covered, inFallback, needed);

            if (config.choices == null) continue;
            for (int c = 0; c < config.choices.Count; c++)
            {
                ChoiceOption choice = config.choices[c];
                if (choice == null) continue;
                CollectNeeded(choice.choiceText, covered, inFallback, needed);
                CollectNeeded(choice.effectText, covered, inFallback, needed);
                CollectNeeded(choice.choiceResult, covered, inFallback, needed);
            }
        }

        for (int i = 0; i < FixedEventStrings.Length; i++)
            CollectNeeded(FixedEventStrings[i], covered, inFallback, needed);

        if (needed.Count == 0)
        {
            PrewarmedFonts.Add(fallbackFont);
            yield break;
        }

        List<uint> chars = new List<uint>(needed);
        int index = 0;
        int batches = 0;
        int missingGlyphCount = 0;

        while (index < chars.Count)
        {
            int count = Mathf.Min(batchSize, chars.Count - index);
            StringBuilder builder = new StringBuilder(count);
            for (int j = 0; j < count; j++)
                builder.Append((char)chars[index + j]);
            index += count;

            string missing;
            bool ok = fallbackFont.TryAddCharacters(builder.ToString(), out missing, false);
            if (!ok && !string.IsNullOrEmpty(missing))
                missingGlyphCount += missing.Length;
            batches++;

            yield return null;
        }

        PrewarmedFonts.Add(fallbackFont);

        int added = chars.Count - missingGlyphCount;
        if (missingGlyphCount > 0)
            Debug.LogWarning($"[EventFontPrewarmer] Prewarmed {added}/{chars.Count} glyph(s) into '{fallbackFont.name}' in {batches} batch(es); {missingGlyphCount} glyph(s) are not present in the source font file.");
        else
            Debug.Log($"[EventFontPrewarmer] Prewarmed {added} glyph(s) into '{fallbackFont.name}' in {batches} batch(es).");
    }

    private static void CollectNeeded(string text, HashSet<uint> covered, HashSet<uint> inFallback, HashSet<uint> needed)
    {
        if (string.IsNullOrEmpty(text)) return;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<')
            {
                i = SkipTag(text, i);
                continue;
            }
            if (c <= ' ') continue;

            uint unicode = c;
            if (covered.Contains(unicode) || inFallback.Contains(unicode)) continue;
            needed.Add(unicode);
        }
    }

    private static int SkipTag(string text, int start)
    {
        int end = text.IndexOf('>', start + 1);
        return end < 0 ? text.Length - 1 : end;
    }

    private static HashSet<uint> GetCharacterSet(TMP_FontAsset font)
    {
        HashSet<uint> set = new HashSet<uint>();
        if (font == null || font.characterTable == null) return set;

        for (int i = 0; i < font.characterTable.Count; i++)
        {
            if (font.characterTable[i] != null)
                set.Add(font.characterTable[i].unicode);
        }
        return set;
    }
}
