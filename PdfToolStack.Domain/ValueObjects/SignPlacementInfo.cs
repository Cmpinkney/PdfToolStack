using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfToolStack.Domain.ValueObjects
{
    public class SignPlacementInfo
    {
        public double CanvasWidth { get; set; }
        public double CanvasHeight { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double SigWidth { get; set; }
        public double SigHeight { get; set; }
    }
}
