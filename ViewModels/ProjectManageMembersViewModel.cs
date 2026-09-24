using AccessEase.Models;

namespace AccessEase12.ViewModels
{
    public class ProjectManageMembersViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";

        public List<AppUser> AllDevelopers { get; set; } = new();
        public List<int> SelectedUserIds { get; set; } = new();
    }
}