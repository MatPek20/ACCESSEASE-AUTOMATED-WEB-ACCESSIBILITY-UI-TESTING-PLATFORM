using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccessEase.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = "";

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = "";

        [Phone]
        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Required]
        public string PasswordHash { get; set; } = "";

        [Required]
        [StringLength(30)]
        public string Role { get; set; } = "Developer";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual List<ScanRecord> ScanRecords { get; set; } = new();

        public virtual List<ProjectMember> ProjectMembers { get; set; } = new();
    }
}