namespace AutomatedClashRunner.Models
{
    public enum SummaryItemType
    {
        Success,
        Warning,
        Error,
        Header
    }

    public class SummaryLineItem
    {
        public string Category { get; set; }
        public string Message { get; set; }
        public SummaryItemType Type { get; set; }

        public string Icon
        {
            get
            {
                switch (Type)
                {
                    case SummaryItemType.Success: return "✓";
                    case SummaryItemType.Warning: return "⚠";
                    case SummaryItemType.Error: return "✗";
                    default: return "•";
                }
            }
        }

        public string ColorHex
        {
            get
            {
                switch (Type)
                {
                    case SummaryItemType.Success: return "#2E7D32"; // Green
                    case SummaryItemType.Warning: return "#F57F17"; // Amber
                    case SummaryItemType.Error: return "#C62828";   // Red
                    default: return "#37474F";                     // Neutral
                }
            }
        }
    }
}
