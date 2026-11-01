using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BusTycoon.AI;
using UnityEngine;
using UnityEngine.Networking;

namespace BusTycoon.Game
{
    /// <summary>
    /// OmniRoute ağ geçidine (OpenAI uyumlu /v1) istek atan küçük istemci. Coroutine olarak çalışır; çağıran
    /// MonoBehaviour'un StartCoroutine'i ile başlatılır. Akış (stream) kullanılmaz: tek parça yanıt beklenir.
    ///
    /// WebGL notu: oyun tarayıcıda açılırsa istek tarayıcıdan gider; OmniRoute'un CORS izin listesinde oyunun
    /// adresi (ör. https://adhbabu.github.io) bulunmalıdır. Masaüstü ve mobil derlemelerde CORS yoktur.
    /// </summary>
    public static class OmniRouteClient
    {
        public const int ChatTimeoutSeconds = 90;
        public const int ProbeTimeoutSeconds = 8;

        /// <summary>Sohbet isteği. Başarıda <paramref name="onReply"/>, aksi halde Türkçe hata metniyle <paramref name="onError"/>.</summary>
        public static IEnumerator Chat(OmniRouteConfig config, IReadOnlyList<ChatMessage> messages, int maxTokens,
            Action<string> onReply, Action<string> onError)
        {
            string body = ChatJson.BuildChatRequest(config.EffectiveModel, messages, maxTokens, 0.4f);
            using (var request = new UnityWebRequest(config.ChatCompletionsUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)) { contentType = "application/json" };
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ChatTimeoutSeconds;
                Prepare(request, config);
                yield return request.SendWebRequest();

                string text = request.downloadHandler?.text;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(Describe(request, text));
                    yield break;
                }
                if (ChatJson.TryParseChatContent(text, out string content, out string error) && !string.IsNullOrEmpty(content))
                    onReply?.Invoke(content);
                else
                    onError?.Invoke(error ?? "Model boş yanıt döndürdü.");
            }
        }

        /// <summary>Bağlantı sınaması: GET /v1/models. Başarıda model kimlikleri listesi döner.</summary>
        public static IEnumerator ListModels(OmniRouteConfig config, Action<List<string>> onModels, Action<string> onError)
        {
            using (var request = UnityWebRequest.Get(config.ModelsUrl))
            {
                request.timeout = ProbeTimeoutSeconds;
                Prepare(request, config);
                yield return request.SendWebRequest();

                string text = request.downloadHandler?.text;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(Describe(request, text));
                    yield break;
                }
                var ids = new List<string>();
                if (ChatJson.TryParseModelIds(text, ids, out string error)) onModels?.Invoke(ids);
                else onError?.Invoke(error);
            }
        }

        static void Prepare(UnityWebRequest request, OmniRouteConfig config)
        {
            request.SetRequestHeader("Accept", "application/json");
            if (config.HasApiKey) request.SetRequestHeader("Authorization", "Bearer " + config.apiKey.Trim());
        }

        /// <summary>Ağ/HTTP hatasını oyuncunun anlayacağı bir cümleye çevirir.</summary>
        static string Describe(UnityWebRequest request, string body)
        {
            long code = request.responseCode;
            string serverMessage = ChatJson.TryParseError(body, out string parsed) ? parsed : null;
            if (request.result == UnityWebRequest.Result.ConnectionError || code == 0)
                return $"OmniRoute'a ulaşılamadı ({Origin(request)}). OmniRoute çalışıyor mu? Masaüstündeki 'OmniRoute Baslat' ile açın.";
            switch (code)
            {
                case 401:
                case 403:
                    return "API anahtarı reddedildi. OmniRoute paneli → Endpoints / API Keys bölümünden anahtarı kopyalayın.";
                case 404:
                    return "Uç nokta bulunamadı. Adres '.../v1' ile bitmeli (ör. http://localhost:20128/v1).";
                case 429:
                    return "Kota doldu ya da çok sık istek atıldı. Biraz bekleyin veya OmniRoute'a başka bir sağlayıcı ekleyin.";
                case 503:
                    return serverMessage ?? "Uygun model yok. OmniRoute paneli → Providers bölümünden en az bir sağlayıcı bağlayın.";
            }
            return serverMessage != null ? $"Hata {code}: {serverMessage}" : $"Hata {code}: {request.error}";
        }

        static string Origin(UnityWebRequest request)
        {
            try { return new Uri(request.url).GetLeftPart(UriPartial.Authority); }
            catch (UriFormatException) { return request.url; }
        }
    }
}
