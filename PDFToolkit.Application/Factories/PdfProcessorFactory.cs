using PDFToolkit.Application.Strategies;
using PDFToolkit.Domain.Enums;

namespace PDFToolkit.Application.Factories
{
    public class PdfProcessorFactory
    {
        private readonly IEnumerable<IProcessingStrategy> _strategies;

        public PdfProcessorFactory(
            IEnumerable<IProcessingStrategy> strategies)
        {
            _strategies = strategies;
        }

        public IProcessingStrategy GetStrategy(ToolType toolType)
        {
            var strategy = _strategies
                .FirstOrDefault(s => s.GetType().Name
                .StartsWith(toolType.ToString()));

            if (strategy == null)
                throw new NotSupportedException(
                    $"No strategy found for tool type: {toolType}");

            return strategy;
        }
    }
}
