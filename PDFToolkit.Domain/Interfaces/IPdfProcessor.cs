using PdfToolkit.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfToolkit.Domain.Interfaces
{
    public interface IPdfProcessor
    {
        ToolType ToolType { get; }

        Task<byte[]> ProcessAsync(
            byte[] fileBytes,
            CancellationToken cancellationToken = default);
    }
}
