namespace PdfToolStack.API.Configuration
{
    public class AnthropicOptions
    {
        public const string SectionName = "Anthropic";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "claude-opus-4-6";
        public int MaxTokens { get; set; } = 2000;
    }
}