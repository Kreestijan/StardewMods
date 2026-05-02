using System.Text;

namespace CutsceneMaker.Importer;

public static class QuoteAwareSplit
{
    public static List<string> Split(string value, char separator)
    {
        ArgumentNullException.ThrowIfNull(value);

        List<string> parts = new();
        StringBuilder current = new();
        bool insideQuotes = false;
        bool escaped = false;

        foreach (char character in value)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\' && insideQuotes)
            {
                current.Append(character);
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                current.Append(character);
                continue;
            }

            if (character == separator && !insideQuotes)
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        parts.Add(current.ToString());
        return parts;
    }
}
