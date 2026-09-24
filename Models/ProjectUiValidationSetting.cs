using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccessEase.Models
{
    public class ProjectUiValidationSetting
    {
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        public bool RequireHeader { get; set; } = true;
        public bool RequireMain { get; set; } = true;
        public bool RequireFooter { get; set; } = true;
        public bool RequireNav { get; set; } = true;
        public bool RequirePageTitle { get; set; } = true;
        public bool RequireSingleH1 { get; set; } = true;

        public bool CheckUnlabeledInputs { get; set; } = true;
        public bool CheckDuplicateIds { get; set; } = true;
        public bool CheckHorizontalOverflow { get; set; } = true;
        public bool CheckTinyClickTargets { get; set; } = true;

        [Range(16, 100)]
        public int MinClickTargetSize { get; set; } = 32;
    }
}