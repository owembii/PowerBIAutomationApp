namespace PowerBIAutomationApp.DTO
{
    internal class CloneSemanticModelDTO
    {
        public required string modelName { get; set; }
        public required string targetWorkspaceId { get; set; }
    }
}
