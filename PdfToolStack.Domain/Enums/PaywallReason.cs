using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfToolStack.Domain.Enums
{
    public enum PaywallReason
    {
        None = 0,
        FileTooLarge = 1,
        AiLimitReached = 2,
        BatchRequiresPro = 3,
        LoginRequired = 4
    }
}
