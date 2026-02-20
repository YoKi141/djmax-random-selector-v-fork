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
            x = x.Replace("'", string.Empty).Replace("-", string.Empty).Replace("ö", "o").Replace("小", "!!!!").ToUpper();
            y = y.Replace("'", string.Empty).Replace("-", string.Empty).Replace("ö", "o").Replace("小", "!!!!").ToUpper();
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
                return 4;
            }
            if (char.IsLetter(ch)) // non-alphabetic letter (e.g. Korean)
            {
                // Korean mode: non-alphabetic titles sort first (before symbols/numbers/A-Z)
                // English mode: non-alphabetic titles sort last (after A-Z, as '#' group at end)
                if (idx == 0)
                    return _gameLanguage == GameLanguage.Korean ? 1 : 5;
                return 5;
            }
            if (char.IsDigit(ch))
            {
                return 3;
            }
            // symbol, punctuation, etc.
            return 2;
        }
    }
}
