namespace Dmrsv.RandomSelector
{
    public class TitleComparer : IComparer<string>
    {
        private readonly GameLanguage _gameLanguage;

        public TitleComparer(GameLanguage gameLanguage = GameLanguage.Korean)
        {
            _gameLanguage = gameLanguage;
        }

        public int Compare(string? x, string? y)
        {
            if (string.Equals(x, y))
            {
                return 0;
            }
            if (y == null)
            {
                return -1;
            }
            if (x == null)
            {
                return 1;
            }
            // Djmax sorts titles with case-insensitive and ignoring the characters below
            x = x.Replace("'", string.Empty).Replace("-", string.Empty).Replace("ö", "o").ToUpper();
            y = y.Replace("'", string.Empty).Replace("-", string.Empty).Replace("ö", "o").ToUpper();
            int index = x.Zip(y, (a, b) => a == b).TakeWhile(equals => equals).Count();
            if (index == Math.Min(x.Length, y.Length))
            {
                return x.Length - y.Length;
            }
            // priority order: white-space -> non-alphabetic letter -> special character -> number -> alphabet
            char a = x[index], b = y[index];
            int priorityA = GetPriority(a, index);
            int priorityB = GetPriority(b, index);
            if (priorityA == priorityB)
            {
                return a.CompareTo(b);
            }
            return priorityA - priorityB;
        }

        private int GetPriority(char ch, int idx)
        {
            if (char.IsWhiteSpace(ch))
            {
                return 0;
            }
            if (char.IsUpper(ch)) // alphabet
            {
                return 5;
            }
            if (char.IsLetter(ch)) // non-alphabetic letter (e.g. Korean, Japanese)
            {
                // Korean mode: Korean first (1), Japanese second (2), other non-Latin with symbols (3)
                // Other modes: non-alphabetic titles sort last (after A-Z, as '#' group at end)
                if (idx == 0)
                {
                    if (_gameLanguage == GameLanguage.Korean)
                    {
                        if (IsHangul(ch)) return 1;
                        if (IsJapanese(ch)) return 2;
                        return 3;
                    }
                    return 2; // non-Korean mode: CJK/non-Latin titles sort before symbols
                }
                return 6;
            }
            if (char.IsDigit(ch))
            {
                return 4;
            }
            // symbol, punctuation, etc.
            return 3;
        }

        private static bool IsHangul(char ch)
            => (ch >= '\uAC00' && ch <= '\uD7FF') // Hangul Syllables
            || (ch >= '\u1100' && ch <= '\u11FF') // Hangul Jamo
            || (ch >= '\u3130' && ch <= '\u318F'); // Hangul Compatibility Jamo

        private static bool IsJapanese(char ch)
            => (ch >= '\u3040' && ch <= '\u30FF') // Hiragana + Katakana
            || (ch >= '\u4E00' && ch <= '\u9FFF') // CJK Unified Ideographs
            || (ch >= '\u3400' && ch <= '\u4DBF'); // CJK Extension A
    }
}
