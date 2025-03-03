namespace PowerBIAutomationApp.DTO
{
    public class SemanticModelListDTO
    {
        public List<SemanticModelDTO> Value { get; set; } = new();
    }

    public class SemanticModelDTO
    {
        public string Id { get; set; }
    }
}
