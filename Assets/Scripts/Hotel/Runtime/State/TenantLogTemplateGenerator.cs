using System.Text;

namespace Hotel.Runtime
{
    public static class TenantLogTemplateGenerator
    {
        public static string Resolve(LogTemplateConfig config, string character, string item, string action)
        {
            if (config == null || config.messageTemplate == null)
                return string.Empty;
            string template = config.messageTemplate;
            var result = new StringBuilder(template.Length);
            for (int i = 0; i < template.Length;)
            {
                if (IsTokenAt(template, i, "%Character%"))
                {
                    result.Append(character ?? string.Empty);
                    i += "%Character%".Length;
                }
                else if (IsTokenAt(template, i, "%item%"))
                {
                    result.Append(item ?? string.Empty);
                    i += "%item%".Length;
                }
                else if (IsTokenAt(template, i, "%action%"))
                {
                    result.Append(action ?? string.Empty);
                    i += "%action%".Length;
                }
                else
                {
                    result.Append(template[i]);
                    i++;
                }
            }
            return result.ToString();
        }

        private static bool IsTokenAt(string text, int index, string token)
        {
            if (index + token.Length > text.Length)
                return false;
            for (int i = 0; i < token.Length; i++)
            {
                if (text[index + i] != token[i])
                    return false;
            }
            return true;
        }

        public static bool RecordBehavior(GameRunState state, string tenantId, LogTemplateConfig config, int day, HotelPhase phase, string character, string item, string action)
        {
            if (state == null || string.IsNullOrEmpty(tenantId) || config == null)
                return false;
            return TenantLogManager.Record(state, new TenantLogWriteDto(
                tenantId,
                config.category,
                day,
                phase,
                Resolve(config, character, item, action),
                config.templateId));
        }
    }
}
