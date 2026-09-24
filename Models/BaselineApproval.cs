using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccessEase.Models
{
    public class BaselineApproval
    {
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [Required]
        [StringLength(500)]
        public string Url { get; set; } = "";

        [Required]
        public int ScanRecordId { get; set; }

        [ForeignKey("ScanRecordId")]
        public virtual ScanRecord? ScanRecord { get; set; }

        [Required]
        public int ApprovedByUserId { get; set; }

        [ForeignKey("ApprovedByUserId")]
        public virtual AppUser? ApprovedByUser { get; set; }

        public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}