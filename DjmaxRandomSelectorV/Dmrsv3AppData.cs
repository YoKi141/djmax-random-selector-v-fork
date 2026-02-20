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
        /// Korean. Used by Locator when GameLanguage is not Korean so that
        /// navigation uses the same title the game displays.
        /// </summary>
        public Dictionary<int, string> EnglishTitles { get; set; } = new();
    }
}
