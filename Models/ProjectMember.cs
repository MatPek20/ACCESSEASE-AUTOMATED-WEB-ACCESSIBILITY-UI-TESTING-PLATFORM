namespace AccessEase.Models
{
    public class ProjectMember
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        public int AppUserId { get; set; }
        public virtual AppUser? AppUser { get; set; }
    }
}