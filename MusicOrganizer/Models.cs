namespace MusicOrganizer
{
    public class PlaylistEntry
    {
        public string RelativePath { get; set; } = "";
        public int Duration { get; set; }
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string? Mood { get; set; }
    }

    public class OverrideEntry
    {
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Mood { get; set; }
    }

    public class KanbanSongItem
    {
        public string FileName { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Title))
                return $"{Artist} - {Title}";
            return FileName;
        }
    }
}
