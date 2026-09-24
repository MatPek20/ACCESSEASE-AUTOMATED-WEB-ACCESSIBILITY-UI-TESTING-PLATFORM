using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccessEase.Models
{
    public class ScanRecord
    {
        public int Id { get; set; }
        public string Url { get; set; } = "";
        public DateTime ScanTime { get; set; }

        public int IssueCount { get; set; }

        public string IssuesJson { get; set; } = "[]";

        public int AppUserId { get; set; }

        [ForeignKey("AppUserId")]
        public virtual AppUser? AppUser { get; set; }

        public int? ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }
    }
}