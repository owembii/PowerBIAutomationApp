namespace PowerBIAutomationApp.DTO
{
    public class SemanticModelListResponse
    {
        public List<SemanticModel> Value { get; set; } = new();
    }

    public class SemanticModel
    {
        public string Id { get; set; }
    }
}
