using System.Linq;
using System.Text;

namespace Gwent.Net
{
    // A player's name as LAN battles show it: up to 16 characters (it fits the battle's side panel), the letters and digits
    // of the Latin alphabets (é, ñ, ß...: the game's fonts have Latin-1, nothing beyond), spaces between words and - _ . ';
    // at least one letter or digit. What is typed is kept to it as it is typed (Typed); a name from elsewhere (the device's,
    // the other player's, a battle found on the network) is made one before it shows (Clean).
    public static class PlayerNames
    {
        public const int MaxLength = 16;
        public const string Rule = "Up to 16 letters, numbers, spaces and - _ . '";

        public static bool Allowed(char c)
        {
            return c <= 0xFF && (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' || c == '\'');
        }

        // What is being typed, kept to the rule: no other characters, no space first or twice, no more than 16 (a space
        // last stays: the next word follows)
        public static string Typed(string text)
        {
            StringBuilder name = new StringBuilder(MaxLength);
            foreach (char c in text ?? "")
            {
                if (name.Length == MaxLength)
                    break;
                if (!Allowed(c) || c == ' ' && (name.Length == 0 || name[name.Length - 1] == ' '))
                    continue;
                name.Append(c);
            }
            return name.ToString();
        }

        // A name to show, kept to the rule and trimmed; null when nothing of it is left (no letter or digit)
        public static string Clean(string text)
        {
            string name = Typed(text).Trim();
            return name.Any(char.IsLetterOrDigit) ? name : null;
        }
    }
}
