using Klimor.WebApi.DXF.Structures;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Services
{
    public class Dxf3DService
    {
        public void Generate3D(List<Coordinates> elements, string filePath)
        {
            DxfDocument dxf = new DxfDocument();

            foreach (var el in elements)
            {
                var p1 = new Vector3(el.x1, el.y1, el.z1);
                var p2 = new Vector3(el.x2, el.y1, el.z1);
                var p3 = new Vector3(el.x2, el.y2, el.z1);
                var p4 = new Vector3(el.x1, el.y2, el.z1);

                var p5 = new Vector3(el.x1, el.y1, el.z2);
                var p6 = new Vector3(el.x2, el.y1, el.z2);
                var p7 = new Vector3(el.x2, el.y2, el.z2);
                var p8 = new Vector3(el.x1, el.y2, el.z2);

                var faces = new[]
                {
                    new Face3D(p1, p2, p3), new Face3D(p1, p3, p4),
                    new Face3D(p5, p6, p7), new Face3D(p5, p7, p8),
                    new Face3D(p1, p2, p6), new Face3D(p1, p6, p5),
                    new Face3D(p4, p3, p7), new Face3D(p4, p7, p8),
                    new Face3D(p1, p4, p8), new Face3D(p1, p8, p5),
                    new Face3D(p2, p3, p7), new Face3D(p2, p7, p6),
                };

                foreach (var f in faces)
                {
                    f.Layer = new Layer(el.label) { Color = AciColor.Yellow };
                    dxf.Entities.Add(f);
                }
            }

            dxf.Save(filePath);
        }
    }
}
