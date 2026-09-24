using System.ComponentModel.DataAnnotations;

namespace AccessEase12.ViewModels
{
    public class BaselineApprovalViewModel
    {
        [Required]
        public int ScanRecordId { get; set; }

        public string? Notes { get; set; }
    }
}