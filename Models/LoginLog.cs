using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccessEase.Models
{
    public class LoginLog
    {
        [Key]
        public int Id { get; set; }

        public int? AppUserId { get; set; }

        [ForeignKey("AppUserId")]
        public virtual AppUser? AppUser { get; set; }

        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = ""; // Success / Failed

        public DateTime LoginTime { get; set; } = DateTime.UtcNow;

        public string IpAddress { get; set; } = "";
    }
}