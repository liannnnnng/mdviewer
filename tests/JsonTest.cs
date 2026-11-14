using System;

namespace MarkdownViewer
{
    // Standalone test harness for JsonLite, compiled with the legacy C# 5 compiler
    // that ships with Windows (no SDK needed). JSON-compat of Escape() output is
    // additionally cross-checked with JSON.parse in Node (run by ../run-tests.sh).
    static class JsonTest
    {
        static int fails = 0, total = 0;

        static void Check(bool ok, string label) { Check(ok, label, null); }

        static void Check(bool ok, string label, string detail)
        {
            total++;
            if (ok) Console.WriteLine("PASS " + label);
            else { fails++; Console.WriteLine("FAIL " + label + (detail != null ? "  => " + detail : "")); }
        }

        static string Esc(string s) { return JsonLite.Escape(s); }

        static void Main()
        {
            string cmd, name, text;

            // --- 1. round trip of nasty content (quotes, backslash, control chars, CJK, emoji, U+2028/9)
            string tricky = "he said \"hi\" back\\slash\nline2\ttab\r\n中文测试😀\u0001ctl\u2028ls\u2029le";
            string j1 = "{\"cmd\":\"save\",\"name\":\"a b.md\",\"text\":" + Esc(tricky) + "}";
            Check(JsonLite.TryParseMessage(j1, out cmd, out name, out text), "roundtrip parse", j1);
            Check(text == tricky, "roundtrip equality", text == null ? "null" : "len " + text.Length + " vs " + tricky.Length);

            // --- 2. "cmd" appearing INSIDE the text value must not fool the parser (naive search would)
            string j2 = "{\"text\":\"prefix \\\"cmd\\\":\\\"fake\\\" suffix\",\"cmd\":\"open\"}";
            JsonLite.TryParseMessage(j2, out cmd, out name, out text);
            Check(cmd == "open" && text == "prefix \"cmd\":\"fake\" suffix", "cmd inside text value", cmd + " | " + text);

            // --- 3. escaped backslash before quote: {"name":"a\\\"b.md"}
            string j3 = "{\"cmd\":\"saveas\",\"name\":\"a\\\\\\\"b.md\",\"text\":null}";
            JsonLite.TryParseMessage(j3, out cmd, out name, out text);
            Check(cmd == "saveas" && name == "a\\\"b.md" && text == null, "backslash-quote + null", name);

            // --- 4. unknown keys, numbers, nested object/array values are skipped
            string j4 = "{\"cmd\":\"drag\",\"x\":42,\"opts\":{\"a\":[1,{\"b\":\"}\"]},\"tail\":[],\"flag\":true,\"n\":null}}";
            Check(JsonLite.TryParseMessage(j4, out cmd, out name, out text) && cmd == "drag", "skip non-string values", cmd);

            // --- 5. reordered keys / empty object
            JsonLite.TryParseMessage("{\"text\":\"t\",\"cmd\":\"max\"}", out cmd, out name, out text);
            Check(cmd == "max" && text == "t", "reordered keys");
            Check(JsonLite.TryParseMessage("{}", out cmd, out name, out text) && cmd == null, "empty object");
            Check(JsonLite.TryParseMessage("  { \"cmd\" : \"open\" }  ", out cmd, out name, out text) && cmd == "open", "whitespace");

            // --- 6. \uXXXX decoding incl. surrogate pair
            JsonLite.TryParseMessage("{\"name\":\"\\u4e2d\\u6587.md\",\"text\":\"\\ud83d\\ude00x\"}", out cmd, out name, out text);
            Check(name == "中文.md" && text == "😀x", "unicode escapes", name + " | " + text);

            // --- 7. Escape basics
            Check(Esc(null) == "null", "escape null");
            Check(Esc("") == "\"\"", "escape empty");
            Check(Esc("plain 123") == "\"plain 123\"", "escape plain");
            Check(Esc("a\"b\\c\nd\te") == "\"a\\\"b\\\\c\\nd\\te\"", "escape specials", Esc("a\"b\\c\nd\te"));
            Check(Esc("\u0001\u001f") == "\"\\u0001\\u001f\"", "escape control chars", Esc("\u0001\u001f"));
            Check(Esc("中文😀") == "\"中文😀\"", "escape keeps unicode raw", Esc("中文😀"));

            // --- 8. round trip through OUR parser of an escaped string containing U+2028 and \/
            string j8 = "{\"text\":" + Esc("a/\\b\u2028c\fd") + "}";
            JsonLite.TryParseMessage(j8, out cmd, out name, out text);
            Check(text == "a/\\b\u2028c\fd", "escape parse back", text);

            // --- 9. malformed input rejected
            Check(!JsonLite.TryParseMessage(null, out cmd, out name, out text), "reject null");
            Check(!JsonLite.TryParseMessage("", out cmd, out name, out text), "reject empty");
            Check(!JsonLite.TryParseMessage("[1,2]", out cmd, out name, out text), "reject array");
            Check(!JsonLite.TryParseMessage("{\"name\":\"unterminated", out cmd, out name, out text), "reject unterminated");
            Check(!JsonLite.TryParseMessage("{\"name\":\"bad\\qescape\"}", out cmd, out name, out text), "reject bad escape");
            Check(!JsonLite.TryParseMessage("{\"name\":\"bad\\u12g4\"}", out cmd, out name, out text), "reject bad hex");
            Check(!JsonLite.TryParseMessage("{\"cmd\":\"x\",}", out cmd, out name, out text), "reject trailing comma");
            Check(!JsonLite.TryParseMessage("{\"cmd\":\"x\" \"name\":\"y\"}", out cmd, out name, out text), "reject missing comma");

            Console.WriteLine();
            Console.WriteLine(fails == 0 ? "ALL PASS (" + total + ")" : fails + "/" + total + " FAILED");
            Environment.Exit(fails == 0 ? 0 : 1);
        }
    }
}
