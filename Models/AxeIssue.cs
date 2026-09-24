using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccessEase.Models
{
    public class AxeIssue
    {
        [Key] // ✅ Added Primary Key to fix the InvalidOperationException
        public int Id { get; set; }

        public string Impact { get; set; }     // critical/serious/moderate/minor
        public string Rule { get; set; }       // e.g. image-alt
        public int Count { get; set; }         // node count
        public string HelpUrl { get; set; }
        public string Help { get; set; }
        public string Target { get; set; }     // css selector
        public string FixTip { get; set; }

        public string HtmlSnippet { get; set; }
        public string EvidenceUrl { get; set; }
        public string ScreenshotUrl { get; set; }
        public int? ApproxHtmlLine { get; set; }

        public string Category { get; set; } = "";

        // ✅ Link to the ScanRecord
        public int ScanId { get; set; }

        [ForeignKey("ScanId")]
        public virtual ScanRecord ScanRecord { get; set; }
    }
}