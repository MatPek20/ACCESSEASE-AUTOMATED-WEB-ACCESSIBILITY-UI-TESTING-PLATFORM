using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccessEase.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? DefaultUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual List<ProjectMember> ProjectMembers { get; set; } = new();
        public virtual List<ScanRecord> ScanRecords { get; set; } = new();
    }
}