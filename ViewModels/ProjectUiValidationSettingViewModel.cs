using System.ComponentModel.DataAnnotations;

namespace AccessEase12.ViewModels
{
    public class ProjectUiValidationSettingViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";

        public bool RequireHeader { get; set; }
        public bool RequireMain { get; set; }
        public bool RequireFooter { get; set; }
        public bool RequireNav { get; set; }
        public bool RequirePageTitle { get; set; }
        public bool RequireSingleH1 { get; set; }

        public bool CheckUnlabeledInputs { get; set; }
        public bool CheckDuplicateIds { get; set; }
        public bool CheckHorizontalOverflow { get; set; }
        public bool CheckTinyClickTargets { get; set; }

        [Range(16, 100)]
        [Display(Name = "Minimum Click Target Size (px)")]
        public int MinClickTargetSize { get; set; } = 32;
    }
}