using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BusTycoon.Core
{
    /// <summary>CSV ayrıştırma ve kalıcı kimlik üretimi (runtime içerik yükleyicisi ve editör içe aktarıcısı ortak kullanır).</summary>
    public static class CsvUtility
    {
        /// <summary>RFC-4180 uyumlu basit CSV ayrıştırıcı (tırnaklı alanlar, kaçışlı "" ve CRLF).</summary>
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': inQuotes = true; break;
                    case ',': fields.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        fields.Add(field.ToString()); field.Clear();
                        if (fields.Count > 1 || fields[0].Length > 0) rows.Add(fields.ToArray());
                        fields.Clear();
                        break;
                    default: field.Append(c); break;
                }
            }

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
            }
            return rows;
        }

        /// <summary>Başlık satırından sütun indeksi; bulunamazsa -1. Birden çok eş anlamlı ad verilebilir.</summary>
        public static int Column(string[] header, params string[] names)
        {
            foreach (var name in names)
                for (int i = 0; i < header.Length; i++)
                    if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public static string Cell(string[] row, int column) =>
            column >= 0 && column < row.Length ? row[column].Trim() : string.Empty;

        public static double ParseDouble(string text, double fallback = 0) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;

        public static long ParseLong(string text) => (long)ParseDouble(text);

        /// <summary>"İstanbul" → "ISTANBUL", "Şanlıurfa" → "SANLIURFA". Kalıcı kimlik üretimi.</summary>
        public static string Slug(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char raw in text.Normalize(NormalizationForm.FormD))
            {
                char c = raw switch
                {
                    'ı' => 'i', 'İ' => 'I', 'ş' => 's', 'Ş' => 'S', 'ğ' => 'g', 'Ğ' => 'G',
                    'ø' => 'o', 'Ø' => 'O', 'ß' => 's', 'æ' => 'a', 'Æ' => 'A', 'ł' => 'l', 'Ł' => 'L',
                    _ => raw
                };
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        /// <summary>Kullanılmış kimliklerle çakışırsa _2, _3 ... ekler.</summary>
        public static string UniqueId(string id, HashSet<string> used)
        {
            string candidate = id;
            for (int n = 2; !used.Add(candidate); n++) candidate = $"{id}_{n}";
            return candidate;
        }
    }
}
