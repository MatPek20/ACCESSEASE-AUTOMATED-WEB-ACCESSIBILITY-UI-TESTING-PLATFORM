using AccessEase.Models;

namespace AccessEase12.ViewModels
{
    public class CompareScansViewModel
    {
        public List<ScanRecord> AvailableScans { get; set; } = new();

        public int? ScanAId { get; set; }
        public int? ScanBId { get; set; }

        public ScanRecord? ScanA { get; set; }
        public ScanRecord? ScanB { get; set; }

        public int ScanAIssueCount { get; set; }
        public int ScanBIssueCount { get; set; }

        public int NewIssuesCount { get; set; }
        public int FixedIssuesCount { get; set; }
        public int UnchangedIssuesCount { get; set; }

        public string ResultStatus { get; set; } = "";

        public List<AxeIssue> NewIssues { get; set; } = new();
        public List<AxeIssue> FixedIssues { get; set; } = new();
        public List<AxeIssue> UnchangedIssues { get; set; } = new();
    }
}