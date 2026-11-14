using System;
using System.Text;

namespace MarkdownViewer
{
    /// <summary>
    /// Minimal JSON helpers for the WebView2 host bridge. We only ever exchange
    /// a flat { "cmd": "...", "name": "...", "text": "..." } object, so pulling in
    /// a full JSON library is not worth its footprint on .NET Framework 4.8.
    /// </summary>
    static class JsonLite
    {
        /// <summary>
        /// Serializes a string as a JSON string literal (quotes included).
        /// The output is also a valid JavaScript string literal, so it can be
        /// embedded directly in code passed to ExecuteScriptAsync.
        /// </summary>
        public static string Escape(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        // Control chars are illegal raw; U+2028/2029 are valid JSON
                        // but break older JS parsers, so escape them too.
                        if (c < ' ' || c == '\u2028' || c == '\u2029')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Parses a flat JSON object whose values are strings or null (the shape
        /// WebView2 produces for window.chrome.webview.postMessage). Unknown keys
        /// are ignored; non-string values are skipped defensively.
        /// </summary>
        public static bool TryParseMessage(string json, out string cmd, out string name, out string text)
        {
            cmd = name = text = null;
            if (string.IsNullOrEmpty(json)) return false;

            int i = 0;
            if (!SkipWs(json, ref i) || json[i] != '{') return false;
            i++;
            bool expectKey = false; // a comma was just consumed; a key (not '}') must follow

            while (true)
            {
                if (!SkipWs(json, ref i)) return false;
                if (json[i] == '}')
                {
                    if (expectKey) return false; // trailing comma
                    i++;
                    return true;
                }
                // next must be a quoted key — whether we just opened '{' or consumed ','
                if (json[i] != '"') return false;

                string key;
                if (!ReadString(json, ref i, out key)) return false;

                if (!SkipWs(json, ref i) || json[i] != ':') return false;
                i++;
                if (!SkipWs(json, ref i)) return false;

                if (json[i] == '"')
                {
                    string val;
                    if (!ReadString(json, ref i, out val)) return false;
                    if (key == "cmd") cmd = val;
                    else if (key == "name") name = val;
                    else if (key == "text") text = val;
                }
                else
                {
                    if (!SkipValue(json, ref i)) return false;
                }

                if (!SkipWs(json, ref i)) return false;
                if (json[i] == ',') { expectKey = true; i++; continue; }
                if (json[i] == '}') { i++; return true; }
                return false;
            }
        }

        /// <summary>Reads a JSON string body starting at the opening quote at i. On failure value stays null.</summary>
        private static bool ReadString(string s, ref int i, out string value)
        {
            value = null;
            i++; // opening quote
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') { value = sb.ToString(); return true; }
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return false;
                char e = s[i++];
                switch (e)
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
                        int cp;
                        if (!ReadHex4(s, ref i, out cp)) return false;
                        sb.Append((char)cp);
                        break;
                    default: return false;
                }
            }
            return false; // unterminated string
        }

        private static bool ReadHex4(string s, ref int i, out int value)
        {
            value = 0;
            if (i + 4 > s.Length) return false;
            for (int k = 0; k < 4; k++)
            {
                char h = s[i++];
                int d;
                if (h >= '0' && h <= '9') d = h - '0';
                else if (h >= 'a' && h <= 'f') d = h - 'a' + 10;
                else if (h >= 'A' && h <= 'F') d = h - 'A' + 10;
                else return false;
                value = (value << 4) | d;
            }
            return true;
        }

        private static bool SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else return true;
            }
            return false;
        }

        /// <summary>Skips a non-string JSON value (null / true / false / number / object / array) starting at i.</summary>
        private static bool SkipValue(string s, ref int i)
        {
            char c = s[i];
            if (c != '{' && c != '[')
            {
                // primitive: ends at the next delimiter
                while (i < s.Length)
                {
                    char ch = s[i];
                    if (ch == ',' || ch == '}' || ch == ']' || ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r')
                        return true;
                    i++;
                }
                return true;
            }

            char open = c, close = (c == '{') ? '}' : ']';
            int depth = 0;
            while (i < s.Length)
            {
                char ch = s[i];
                if (ch == '"')
                {
                    // honor strings so braces/brackets inside them don't count
                    string junk;
                    if (!ReadString(s, ref i, out junk)) return false;
                    continue;
                }
                if (ch == open) depth++;
                else if (ch == close)
                {
                    depth--;
                    if (depth == 0) { i++; return true; }
                }
                i++;
            }
            return false;
        }
    }
}
