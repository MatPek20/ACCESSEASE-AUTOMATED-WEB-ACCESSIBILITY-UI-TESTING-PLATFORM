using System.ComponentModel.DataAnnotations;

namespace AccessEase12.ViewModels
{
    public class ProjectFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Project Name")]
        public string Name { get; set; } = "";

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Default Website URL")]
        [Url]
        [StringLength(500)]
        public string? DefaultUrl { get; set; }
    }
}