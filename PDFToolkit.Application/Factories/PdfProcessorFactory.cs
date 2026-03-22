using PdfToolkit.Domain.Enums;
using PdfToolkit.Application.Strategies;

namespace PdfToolkit.Application.Factories
{
    public class PdfProcessorFactory
    {
        private readonly IEnumerable<IProcessingStrategy> _strategies;

        public PdfProcessorFactory(
            IEnumerable<IProcessingStrategy> strategies)
        {
            _strategies = strategies;

            // Log what strategies are registered
            Console.WriteLine($"Factory created with {_strategies.Count()} strategies:");
            foreach (var s in _strategies)
                Console.WriteLine($"  - {s.GetType().Name} handles {s.ToolType}");
        }

        public IProcessingStrategy GetStrategy(ToolType toolType)
        {
            Console.WriteLine($"Looking for strategy: {toolType}");

            foreach (var s in _strategies)
                Console.WriteLine($"  Checking: {s.GetType().Name} — ToolType: {s.ToolType}");

            var strategy = _strategies
                .FirstOrDefault(s => s.ToolType == toolType);

            if (strategy == null)
                throw new NotSupportedException(
                    $"No strategy found for tool type: {toolType}. " +
                    $"Registered: {string.Join(", ", _strategies.Select(s => $"{s.GetType().Name}={s.ToolType}"))}");

            return strategy;
        }
    }
}