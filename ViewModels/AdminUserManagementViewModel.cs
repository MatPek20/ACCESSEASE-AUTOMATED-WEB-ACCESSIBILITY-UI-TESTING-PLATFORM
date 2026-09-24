using System.ComponentModel.DataAnnotations;

namespace AccessEase12.ViewModels
{
    public class AdminUserManagementViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Full Name")]
        public string FullName { get; set; } = "";

        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Display(Name = "Role")]
        public string Role { get; set; } = "";

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }
    }
}