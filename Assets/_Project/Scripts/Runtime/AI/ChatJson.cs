using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BusTycoon.AI
{
    public enum ChatRole { System, User, Assistant }

    public readonly struct ChatMessage
    {
        public readonly ChatRole Role;
        public readonly string Content;

        public ChatMessage(ChatRole role, string content)
        {
            Role = role;
            Content = content ?? string.Empty;
        }

        public string RoleName => Role switch { ChatRole.System => "system", ChatRole.Assistant => "assistant", _ => "user" };
    }

    /// <summary>
    /// OpenAI uyumlu /chat/completions isteği ve yanıtı için küçük JSON yardımcıları.
    /// JsonUtility iç içe dizileri ve isteğe bağlı alanları iyi çözemediği için kendi küçük ayrıştırıcımız var;
    /// Unity'ye bağımlı değildir (EditMode testleriyle sınanır).
    /// </summary>
    public static class ChatJson
    {
        // ------------------------------------------------------------------ istek

        public static string BuildChatRequest(string model, IReadOnlyList<ChatMessage> messages, int maxTokens, float temperature)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"model\":");
            AppendString(sb, string.IsNullOrWhiteSpace(model) ? OmniRouteConfig.DefaultModel : model.Trim());
            sb.Append(",\"stream\":false");
            if (maxTokens > 0) sb.Append(",\"max_tokens\":").Append(maxTokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"temperature\":").Append(temperature.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append(",\"messages\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"role\":");
                AppendString(sb, messages[i].RoleName);
                sb.Append(",\"content\":");
                AppendString(sb, messages[i].Content);
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ yanıt

        /// <summary>choices[0].message.content (yoksa choices[0].text). Hata gövdesinde error.message döner.</summary>
        public static bool TryParseChatContent(string json, out string content, out string error)
        {
            content = null;
            error = null;
            if (!TryParse(json, out object root) || !(root is Dictionary<string, object> obj))
            {
                error = "Yanıt okunamadı (JSON değil).";
                return false;
            }
            if (TryGetError(obj, out error)) return false;

            if (obj.TryGetValue("choices", out object choicesValue) && choicesValue is List<object> choices && choices.Count > 0 &&
                choices[0] is Dictionary<string, object> choice)
            {
                if (choice.TryGetValue("message", out object messageValue) && messageValue is Dictionary<string, object> message &&
                    message.TryGetValue("content", out object text) && text is string messageText)
                {
                    content = messageText.Trim();
                    return true;
                }
                if (choice.TryGetValue("text", out object legacy) && legacy is string legacyText)
                {
                    content = legacyText.Trim();
                    return true;
                }
            }
            error = "Yanıtta metin yok.";
            return false;
        }

        /// <summary>/v1/models yanıtındaki data[].id listesi.</summary>
        public static bool TryParseModelIds(string json, List<string> ids, out string error)
        {
            ids.Clear();
            error = null;
            if (!TryParse(json, out object root) || !(root is Dictionary<string, object> obj))
            {
                error = "Yanıt okunamadı (JSON değil).";
                return false;
            }
            if (TryGetError(obj, out error)) return false;
            if (!obj.TryGetValue("data", out object dataValue) || !(dataValue is List<object> data))
            {
                error = "Model listesi yok.";
                return false;
            }
            foreach (object item in data)
                if (item is Dictionary<string, object> model && model.TryGetValue("id", out object id) && id is string text && text.Length > 0)
                    ids.Add(text);
            return true;
        }

        /// <summary>{"error":{"message":"..."}} ya da {"error":"..."} biçimindeki hata.</summary>
        public static bool TryParseError(string json, out string error)
        {
            error = null;
            return TryParse(json, out object root) && root is Dictionary<string, object> obj && TryGetError(obj, out error);
        }

        static bool TryGetError(Dictionary<string, object> obj, out string error)
        {
            error = null;
            if (!obj.TryGetValue("error", out object value) || value == null) return false;
            if (value is string text) error = text;
            else if (value is Dictionary<string, object> detail && detail.TryGetValue("message", out object message) && message is string m) error = m;
            else error = "Bilinmeyen hata.";
            return true;
        }

        // ------------------------------------------------------------------ küçük JSON ayrıştırıcı

        /// <summary>JSON → Dictionary&lt;string, object&gt; / List&lt;object&gt; / string / double / bool / null.</summary>
        public static bool TryParse(string json, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(json)) return false;
            int i = 0;
            try
            {
                value = ParseValue(json, ref i);
                SkipWhitespace(json, ref i);
                return i == json.Length;
            }
            catch (FormatException)
            {
                value = null;
                return false;
            }
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException();
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref i);
                    throw new FormatException();
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException();
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException();
                i++;
                result[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException();
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw new FormatException();
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException();
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw new FormatException();
            }
        }

        static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= s.Length) break;
                char esc = s[i++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException();
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException();
                }
            }
            throw new FormatException();
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                throw new FormatException();
            return number;
        }

        static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException();
            i += word.Length;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
