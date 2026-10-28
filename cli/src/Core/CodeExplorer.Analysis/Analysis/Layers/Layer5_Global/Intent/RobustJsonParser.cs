using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Robust, fault-tolerant JSON parser designed specifically for extracting and healing
/// structured responses from LLMs (Gemini, Claude, GPT-4, Ollama, DeepSeek).
/// Handles markdown fences, &lt;think&gt; tags, conversational preambles/postambles,
/// comments, trailing commas, and auto-completes prematurely cut-off brackets/quotes.
/// </summary>
public static class RobustJsonParser
{
    private static readonly JsonSerializerOptions PermissiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static T? Deserialize<T>(string? rawText, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return default;

        var healed = CleanAndHealJson(rawText);
        if (string.IsNullOrWhiteSpace(healed))
            return default;

        return JsonSerializer.Deserialize<T>(healed, options ?? PermissiveOptions);
    }

    public static bool TryDeserialize<T>(string? rawText, out T? result, JsonSerializerOptions? options = null)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(rawText))
            return false;

        try
        {
            var healed = CleanAndHealJson(rawText);
            if (string.IsNullOrWhiteSpace(healed))
                return false;

            result = JsonSerializer.Deserialize<T>(healed, options ?? PermissiveOptions);
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    public static string CleanAndHealJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var text = input.Trim();

        // 1. Strip reasoning/thinking tags (e.g. <think>...</think> or unclosed <think>...)
        text = Regex.Replace(text, @"<think>[\s\S]*?(?:</think>|$)", "", RegexOptions.IgnoreCase).Trim();

        // 2. Strip Markdown code fences: ```json ... ``` or ``` ... ```
        var fenceMatch = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (fenceMatch.Success)
        {
            text = fenceMatch.Groups[1].Value.Trim();
        }
        else if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                text = text[(firstNewLine + 1)..].Trim();
            }
            if (text.EndsWith("```", StringComparison.Ordinal))
            {
                text = text[..^3].Trim();
            }
        }

        // 3. Find outer JSON bounds: look for first '{' or '['
        var firstBrace = text.IndexOf('{');
        var firstBracket = text.IndexOf('[');

        int startIdx;
        char openChar, closeChar;

        if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
        {
            startIdx = firstBrace;
            openChar = '{';
            closeChar = '}';
        }
        else if (firstBracket >= 0)
        {
            startIdx = firstBracket;
            openChar = '[';
            closeChar = ']';
        }
        else
        {
            return text;
        }

        text = text[startIdx..];

        // 4. Strip single-line (// ...) and multi-line (/* ... */) comments outside of strings
        text = StripJsonComments(text);

        // 5. Remove trailing commas before closing braces/brackets: , \s* } -> }
        text = Regex.Replace(text, @",\s*([}\]])", "$1");

        // 6. Heal truncated JSON: ensure balanced quotes, braces, and brackets
        text = HealTruncation(text, openChar, closeChar);

        return text;
    }

    private static string StripJsonComments(string json)
    {
        var sb = new StringBuilder(json.Length);
        var inString = false;
        var escape = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (escape)
            {
                sb.Append(c);
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                sb.Append(c);
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                sb.Append(c);
                continue;
            }

            if (!inString)
            {
                if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
                {
                    i += 2;
                    while (i < json.Length && json[i] != '\n' && json[i] != '\r')
                    {
                        i++;
                    }
                    if (i < json.Length && (json[i] == '\n' || json[i] == '\r'))
                    {
                        sb.Append(json[i]);
                    }
                    continue;
                }
                if (c == '/' && i + 1 < json.Length && json[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/'))
                    {
                        i++;
                    }
                    i++;
                    continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string HealTruncation(string json, char primaryOpen, char primaryClose)
    {
        var stack = new Stack<char>();
        var inString = false;
        var escape = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c is '{' or '[')
            {
                stack.Push(c);
            }
            else if (c is '}' or ']')
            {
                if (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if ((c == '}' && top == '{') || (c == ']' && top == '['))
                    {
                        stack.Pop();
                        if (stack.Count == 0)
                        {
                            // Closed the root container; trim any trailing postamble commentary
                            return json[..(i + 1)];
                        }
                    }
                }
            }
        }

        var sb = new StringBuilder(json);
        if (inString)
        {
            sb.Append('"');
        }

        var current = sb.ToString();
        current = Regex.Replace(current, @",\s*$", "");
        sb = new StringBuilder(current);

        while (stack.Count > 0)
        {
            var open = stack.Pop();
            sb.Append(open == '{' ? '}' : ']');
        }

        return sb.ToString();
    }
}
