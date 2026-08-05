using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Prewarms the dynamic TMP fallback font with every glyph the
/// TenantReviewPanel can display (candidate names + detailed descriptions +
/// the fixed label/reject strings shown by TenantReviewPanel.Show).
///
/// The panel's primary font (NotoSansSC-VF SDF) is static, so any glyph it
/// does not contain is resolved at runtime through its dynamic fallback
/// (NotoSansSC-VF SDF_FallBack), triggering an atlas rebuild on first use.
/// Prewarming that fallback during scene start removes the hitch from the
/// first panel show. Runs once per session per font.
///
/// Activation/popup architecture is intentionally untouched.
/// </summary>
public class TenantReviewFontPrewarmer : MonoBehaviour
{
    [Header("Fonts")]
    [Tooltip("Static primary font (NotoSansSC-VF SDF). Glyphs it already contains are skipped.")]
    [SerializeField] private TMP_FontAsset primaryFont;

    [Tooltip("Dynamic fallback font to prewarm (NotoSansSC-VF SDF_FallBack). Must be Dynamic.")]
    [SerializeField] private TMP_FontAsset fallbackFont;

    [Header("Tuning")]
    [Tooltip("Max glyphs added per TryAddCharacters batch before yielding one frame.")]
    [SerializeField, Min(1)] private int batchSize = 64;

    // Fixed strings rendered by TenantReviewPanel.Show. Must mirror its literals.
    private static readonly string[] FixedPanelStrings =
    {
        // Ability labels (GetAbilityLabel).
        "医生", "厨师", "工程师", "守夜人", "前员工", "商贩", "木工", "农民", "无标签",
        // Activity labels (GetActivityLabel).
        "夜行", "全天", "日行",
        // Short-line prefix and recruit rejection reason.
        "能力：", "活跃：", "旅馆没有空房，无法招募。",
    };

    // Session-scoped guard: re-entering the scene without a domain reload must
    // not prewarm the same font twice (runtime additions persist in that case).
    private static readonly HashSet<TMP_FontAsset> PrewarmedFonts = new HashSet<TMP_FontAsset>();

    private void Start()
    {
        StartCoroutine(PrewarmRoutine());
    }

    private IEnumerator PrewarmRoutine()
    {
        if (primaryFont == null || fallbackFont == null)
        {
            Debug.LogWarning("[TenantReviewFontPrewarmer] Missing font reference(s). Assign primaryFont and fallbackFont in the inspector; prewarm skipped.");
            yield break;
        }

        if (fallbackFont.atlasPopulationMode != AtlasPopulationMode.Dynamic)
        {
            Debug.LogWarning($"[TenantReviewFontPrewarmer] Target font '{fallbackFont.name}' is not dynamic (mode: {fallbackFont.atlasPopulationMode}); prewarm skipped.");
            yield break;
        }

        if (PrewarmedFonts.Contains(fallbackFont))
            yield break; // Already prewarmed this session.

        var coordinator = GetComponent<TenantReviewCoordinator>();
        if (coordinator == null || coordinator.candidates == null || coordinator.candidates.Count == 0)
        {
            Debug.LogWarning("[TenantReviewFontPrewarmer] No TenantReviewCoordinator with candidates found on this GameObject; prewarm skipped.");
            yield break;
        }

        // Glyphs already covered by either font are skipped.
        HashSet<uint> covered = GetCharacterSet(primaryFont);
        HashSet<uint> inFallback = GetCharacterSet(fallbackFont);
        HashSet<uint> needed = new HashSet<uint>();

        for (int i = 0; i < coordinator.candidates.Count; i++)
        {
            TenantReviewCandidateSO candidate = coordinator.candidates[i];
            if (candidate == null) continue;
            CollectNeeded(candidate.displayName, covered, inFallback, needed);
            CollectNeeded(candidate.detailedDescription, covered, inFallback, needed);
        }

        for (int i = 0; i < FixedPanelStrings.Length; i++)
            CollectNeeded(FixedPanelStrings[i], covered, inFallback, needed);

        if (needed.Count == 0)
        {
            PrewarmedFonts.Add(fallbackFont);
            yield break; // Nothing to add.
        }

        // Prewarm in bounded batches, yielding once per batch.
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
            Debug.LogWarning($"[TenantReviewFontPrewarmer] Prewarmed {added}/{chars.Count} glyph(s) into '{fallbackFont.name}' in {batches} batch(es); {missingGlyphCount} glyph(s) are not present in the source font file.");
        else
            Debug.Log($"[TenantReviewFontPrewarmer] Prewarmed {added} glyph(s) into '{fallbackFont.name}' in {batches} batch(es).");
    }

    /// <summary>
    /// Adds every display glyph of <paramref name="text"/> that is neither
    /// covered by the primary font nor already present in the fallback font.
    /// Rich text tags and control/space characters are skipped.
    /// </summary>
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
            if (c <= ' ') continue; // Space, newline, tabs, control chars.

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
