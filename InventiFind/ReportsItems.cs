using System;

namespace InventiFind
{
    public class ReportsItems
    {
        public int ReportId { get; set; }

        public int UserId { get; set; }

        public string ReportType { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string Status { get; set; } = "open";

        public DateTime DateReported { get; set; }

        public string ReporterName { get; set; } = string.Empty;
    }
}