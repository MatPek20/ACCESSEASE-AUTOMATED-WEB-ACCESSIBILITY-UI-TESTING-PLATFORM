using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccessEase.Models
{
    public class RemediationIssue
    {
        public int Id { get; set; }

        public int ScanRecordId { get; set; }

        [ForeignKey("ScanRecordId")]
        public virtual ScanRecord? ScanRecord { get; set; }

        [Required]
        [StringLength(50)]
        public string Impact { get; set; } = "";

        [Required]
        [StringLength(150)]
        public string Rule { get; set; } = "";

        public string? Target { get; set; }

        public string? FixTip { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Fixed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}