namespace Student_Information_system.Services
{
    public static class ViewHelpers
    {
        public static string FileSize(long bytes) => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
            _ => $"{bytes / 1024.0 / 1024.0:0.0} MB"
        };

        public static string Ago(DateTime t)
        {
            var s = DateTime.Now - t;
            if (s.TotalMinutes < 1) return "just now";
            if (s.TotalHours < 1) return $"{(int)s.TotalMinutes} min ago";
            if (s.TotalDays < 1) return $"{(int)s.TotalHours} h ago";
            if (s.TotalDays < 7) return $"{(int)s.TotalDays} d ago";
            return t.ToString("d MMM yyyy");
        }

        public static string KindIcon(string kind) => kind switch
        {
            "Slides" => "easel", "Assignment" => "clipboard-check", "Video" => "play-btn", "Link" => "link-45deg", _ => "file-earmark-text"
        };
    }
}
