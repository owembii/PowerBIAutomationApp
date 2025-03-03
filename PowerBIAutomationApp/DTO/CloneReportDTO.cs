namespace PowerBIAutomationApp.DTO
{
    internal class CloneReportDTO
    {
        public string? reportName { get; set; }
        public string? targetWorkspaceId { get; set; }
        public string? targetSemanticModelId { get; set; }
    }
}
