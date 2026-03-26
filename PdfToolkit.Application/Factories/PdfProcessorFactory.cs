using Microsoft.Extensions.Logging;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Application.Strategies;

namespace PdfToolkit.Application.Factories
{
    public class PdfProcessorFactory
    {
        private readonly IEnumerable<IProcessingStrategy> _strategies;
        private readonly ILogger<PdfProcessorFactory> _logger;

        public PdfProcessorFactory(
            IEnumerable<IProcessingStrategy> strategies,
            ILogger<PdfProcessorFactory> logger)
        {
            _strategies = strategies;
            _logger = logger;

            _logger.LogInformation("Factory created with {Count} strategies", _strategies.Count());

            foreach (var s in _strategies)
            {
                _logger.LogInformation(
                    "Registered strategy: {Strategy} handles {ToolType}",
                    s.GetType().Name,
                    s.ToolType);
            }
        }

        public IProcessingStrategy GetStrategy(ToolType toolType)
        {
            _logger.LogInformation("Looking for strategy: {ToolType}", toolType);

            foreach (var s in _strategies)
            {
                _logger.LogDebug(
                    "Checking strategy: {Strategy} — ToolType: {ToolType}",
                    s.GetType().Name,
                    s.ToolType);
            }

            var strategy = _strategies
                .FirstOrDefault(s => s.ToolType == toolType);

            if (strategy == null)
            {
                _logger.LogError("No strategy found for {ToolType}", toolType);

                throw new NotSupportedException(
                    $"No strategy found for tool type: {toolType}. " +
                    $"Registered: {string.Join(", ", _strategies.Select(s => $"{s.GetType().Name}={s.ToolType}"))}");
            }

            return strategy;
        }
    }
}