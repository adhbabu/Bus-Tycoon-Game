using System;

namespace BusTycoon.AI
{
    /// <summary>
    /// OmniRoute yapay zekâ ağ geçidi bağlantı ayarları (https://github.com/diegosouzapw/OmniRoute).
    /// OmniRoute; Claude, GPT, Gemini ve ücretsiz modelleri tek bir OpenAI uyumlu uç noktada (/v1) toplar.
    /// Varsayılan: oyuncunun kendi bilgisayarında çalışan <c>http://localhost:20128/v1</c>.
    ///
    /// Güvenlik kuralı: API anahtarı ASLA derlemeye gömülmez; yalnızca oyuncunun cihazında (PlayerPrefs) saklanır.
    /// </summary>
    [Serializable]
    public sealed class OmniRouteConfig
    {
        public const string DefaultBaseUrl = "http://localhost:20128/v1";
        /// <summary>"auto": OmniRoute bağlı sağlayıcılar arasından uygun modeli kendisi seçer.</summary>
        public const string DefaultModel = "auto";

        public bool enabled;
        public string baseUrl = DefaultBaseUrl;
        public string apiKey = string.Empty;
        public string model = DefaultModel;

        public OmniRouteConfig Clone() => new OmniRouteConfig { enabled = enabled, baseUrl = baseUrl, apiKey = apiKey, model = model };

        /// <summary>Model boşsa "auto".</summary>
        public string EffectiveModel => string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();

        public bool HasApiKey => !string.IsNullOrWhiteSpace(apiKey);

        /// <summary>
        /// Kullanıcının yazdığı adresi düzeltir: boşluklar ve sondaki "/" atılır, şema yoksa http eklenir,
        /// "/v1" yoksa eklenir ("localhost:20128" → "http://localhost:20128/v1"). Sonda "/chat/completions"
        /// gibi bir yol yapıştırıldıysa o da temizlenir.
        /// </summary>
        public static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return DefaultBaseUrl;
            string result = url.Trim();
            if (!result.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !result.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                result = "http://" + result;
            result = result.TrimEnd('/');

            foreach (string suffix in new[] { "/chat/completions", "/models" })
                if (result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    result = result.Substring(0, result.Length - suffix.Length).TrimEnd('/');

            if (!result.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) result += "/v1";
            return result;
        }

        public string ChatCompletionsUrl => NormalizeBaseUrl(baseUrl) + "/chat/completions";
        public string ModelsUrl => NormalizeBaseUrl(baseUrl) + "/models";
    }
}
