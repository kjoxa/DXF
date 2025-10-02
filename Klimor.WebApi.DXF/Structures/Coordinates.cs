using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Structures
{
    public class Coordinates
    {
        public int x1 { get; set; }

        public int x2 { get; set; }

        public int y1 { get; set; }

        public int y2 { get; set; }

        public int z1 { get; set; }

        public int z2 { get; set; }

        public int PositionUp { get; set; }

        public int PositionDown { get; set; }

        public string label { get; set; }

        public string posUpDown { get; set; }

        public string type { get; set; }

        public AdditionalInfo additionalInfos { get; set; }
    }

    public class AdditionalInfo
    {
        // ogólne
        public string sName { get; set; }

        public string airPath { get; set; }

        // ikony
        public string iconPosition { get; set; }

        public string iconName { get; set; }

        // bloki
        public int positionUp { get; set; }

        public int positionDown { get; set; }

        public int blockNumber { get; set; }

        // external elements
        public string direction { get; set; }

        public string airPathPosition { get; set; }
    }
}
