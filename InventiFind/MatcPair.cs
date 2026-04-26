namespace InventiFind;

public class MatchPair
{
    public int LostId { get; set; }
    public int SurrenderedId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string SubmittedDate { get; set; } = string.Empty;
    public string LostReportNo { get; set; } = string.Empty;
}