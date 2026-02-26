using DjmaxRandomSelectorV.Models;
using System.Collections.Generic;

namespace DjmaxRandomSelectorV
{
    public class Dmrsv3AppData
    {
        public string[] CategoryType { get; set; }
        public string[] BasicCategories { get; set; }
        public Category[] Categories { get; set; }
        public PliCategory[] PliCategories { get; set; }
        public LinkDiscItem[] LinkDisc { get; set; }
        /// <summary>
        /// Maps track ID → English title for tracks whose original title is
        /// Korean. Used by Locator when GameLanguage is English.
        /// </summary>
        public Dictionary<int, string> EnglishTitles { get; set; } = new();
        /// <summary>
        /// Maps track ID → Japanese title for tracks whose original title is
        /// Korean. Used by Locator when GameLanguage is Japanese.
        /// </summary>
        public Dictionary<int, string> JapaneseTitles { get; set; } = new();
    }
}
