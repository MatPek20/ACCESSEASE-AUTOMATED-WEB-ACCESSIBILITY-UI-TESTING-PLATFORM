namespace AccessEase.Models
{
    public class CiCdScanRequest
    {
        public string Token { get; set; } = "";
        public int ProjectId { get; set; }
        public string Url { get; set; } = "";
        public string Mode { get; set; } = "axe";
        public string Severity { get; set; } = "all";
    }
}