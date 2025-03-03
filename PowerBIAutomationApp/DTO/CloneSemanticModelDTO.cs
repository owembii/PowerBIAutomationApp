namespace PowerBIAutomationApp.DTO
{
    internal class CloneSemanticModelDTO
    {
        public required string semanticModelName { get; set; }
        public required string targetWorkspaceId { get; set; }
    }
}
