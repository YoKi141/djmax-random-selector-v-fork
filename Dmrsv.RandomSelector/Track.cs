namespace Dmrsv.RandomSelector
{
    public record Track
    {
        public MusicInfo Info { get; init; } = new();
        public Pattern[] Patterns { get; init; } = Array.Empty<Pattern>();
        public bool IsPlayable { get; init; } = false;
        /// <summary>
        /// English title for non-Korean game language navigation.
        /// Null when the original title is already used for all languages.
        /// </summary>
        public string? TitleEn { get; init; } = null;

        public int Id => Info.Id;
        public string Title => Info.Title;
        public string Composer => Info.Composer;
        public string Category => Info.Category;
    }
}
