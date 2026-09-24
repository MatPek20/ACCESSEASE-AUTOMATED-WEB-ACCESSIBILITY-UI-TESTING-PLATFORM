using System.ComponentModel.DataAnnotations;

namespace AccessEase12.ViewModels
{
    public class RemediationUpdateViewModel
    {
        public int Id { get; set; }

        [Required]
        public string Status { get; set; } = "";
    }
}