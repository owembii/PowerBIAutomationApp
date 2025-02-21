namespace PowerBIAutomationApp.DTO
{
    internal class DeleteReportDTO
    {
        public required string WorkspaceId { get; set; }
        public required string ReportName { get; set; }
    }
}
