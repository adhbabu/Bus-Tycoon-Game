using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Başlatma seçenekleri — oyuncu ayarı değil, test ve mağaza görselleri içindir.
    /// Web'de adres çubuğundaki sorgudan (…/oyna/?profil=telefon), diğer platformlarda komut satırından
    /// (-profil telefon) okunur.
    ///  profil = telefon | tablet | masaustu → arayüz düzenini zorlar (ör. bilgisayarda telefon görünümü)
    ///  vitrin                               → mağaza ekran görüntüsü turu (<see cref="ShowcaseDirector"/>)
    /// </summary>
    public static class LaunchOptions
    {
        static Dictionary<string, string> values;

        /// <summary>Seçeneğin değeri; seçenek yoksa null, değersiz bayraksa boş metin.</summary>
        public static string Get(string key)
        {
            Load();
            return values.TryGetValue(key, out string value) ? value : null;
        }

        public static bool Has(string key) => Get(key) != null;

        static void Load()
        {
            if (values != null) return;
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Application.platform == RuntimePlatform.WebGLPlayer) ParseQuery(Application.absoluteURL);
            else ParseCommandLine(Environment.GetCommandLineArgs());
        }

        static void ParseQuery(string url)
        {
            int start = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (start < 0) return;
            int end = url.IndexOf('#', start);
            string query = end < 0 ? url.Substring(start + 1) : url.Substring(start + 1, end - start - 1);

            foreach (string pair in query.Split('&'))
            {
                if (pair.Length == 0) continue;
                int equals = pair.IndexOf('=');
                string key = Decode(equals < 0 ? pair : pair.Substring(0, equals));
                values[key] = equals < 0 ? string.Empty : Decode(pair.Substring(equals + 1));
            }
        }

        static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

        static void ParseCommandLine(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].Length < 2 || args[i][0] != '-') continue;
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal);
                values[args[i].Substring(1)] = hasValue ? args[++i] : string.Empty;
            }
        }
    }
}
