using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Structures
{
    public class FirstStepInput
    {
        public double SupAirVolFlow { get; set; }

        public double ExhAirVolFlow { get; set; }

        public double SupAirPressDrop { get; set; }

        public double ExhAirPressDrop { get; set; }

        public string AhuType { get; set; }

        public string AhuTypeOryginal { get; set; }

        public string AhuPurpose { get; set; }

        public string AhuKind { get; set; }

        public string AhuOrientation { get; set; }

        public string SupAhuSide { get; set; }

        public string ExhAhuSide { get; set; }

        public string AhuModel { get; set; }

        public string AhuSetup { get; set; }

        public bool IsSingleFunctionCalculation { get; set; }
    }
}
