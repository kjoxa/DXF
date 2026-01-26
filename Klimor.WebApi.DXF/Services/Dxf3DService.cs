using Klimor.WebApi.DXF.Consts;
using Klimor.WebApi.DXF.Structures;
using netDxf;
using netDxf.Collections;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Services
{
    public class Dxf3DService
    {
        public ViewsList Views;

        public Dxf3DService()
        {
            Views = new ViewsList();
        }

        public void Generate3D(List<Coordinates> elements, string filePath)
        {
            Views = new ViewsList();
            var dxf = new DxfDocument();

            Views.AhuLength = elements.Where(el => el.label == Lab.Block).Max(e => e.x2);
            Views.AhuHeight = elements.Where(el => el.label == Lab.Block).Max(e => e.y2);
            Views.AhuWidth = elements.Where(el => el.label == Lab.Block).Max(e => e.z2);

            var blocksLayer = dxf.Layers.Add(new Layer(Lab.Block) { Color = new AciColor(4) });
            var functionsLayer = dxf.Layers.Add(new Layer(Lab.Function) { Color = new AciColor(3) });
            var operationalLayer = dxf.Layers.Add(new Layer(Lab.Operational) { Color = new AciColor(2) });
            var backLayer = dxf.Layers.Add(new Layer(Lab.Back) { Color = new AciColor(1) });
            var upLayer = dxf.Layers.Add(new Layer(Lab.Up) { Color = new AciColor(7) });
            var downLayer = dxf.Layers.Add(new Layer(Lab.Down) { Color = new AciColor(6) });
            var holeLayer = dxf.Layers.Add(new Layer(Lab.Hole) { Color = new AciColor(5) });
            var airDamperLayer = dxf.Layers.Add(new Layer(Lab.AD) { Color = new AciColor(9) });
            var flexibleConnectionLayer = dxf.Layers.Add(new Layer(Lab.FC) { Color = new AciColor(10) });
            var intakeOutletLayer = dxf.Layers.Add(new Layer(Lab.INTK) { Color = new AciColor(11) });
            var iconLayer = dxf.Layers.Add(new Layer(Lab.Icon) { Color = new AciColor(7) });

            var icons = DxfDocument.Load("BLOCKS.dxf");
            var iconsList = icons.Blocks.ToList();

            if (elements == null || elements.Count == 0) return;

            // raz przed foreachem:
            double minX = double.PositiveInfinity,
                   minY = double.PositiveInfinity,
                   zFront = double.NegativeInfinity;

            foreach (var e in elements)
            {
                minX = Math.Min(minX, Math.Min(e.x1, e.x2));
                minY = Math.Min(minY, Math.Min(e.y1, e.y2));
                zFront = Math.Max(zFront, Math.Max(e.z1, e.z2)); // jeśli u "front" zawsze = z2 -> Math.Max(zFront, e.z2)
            }

            // skróty (żeby w pętli było czytelnie)
            double X(double x) => x - minX;
            double Y(double y) => y - minY;
            double Z(double z) => zFront - z;

            foreach (var el in elements)
            {
                var p1 = new Vector3(X(el.x1), Y(el.y1), Z(el.z1));
                var p2 = new Vector3(X(el.x2), Y(el.y1), Z(el.z1));
                var p3 = new Vector3(X(el.x2), Y(el.y2), Z(el.z1));
                var p4 = new Vector3(X(el.x1), Y(el.y2), Z(el.z1));

                var p5 = new Vector3(X(el.x1), Y(el.y1), Z(el.z2));
                var p6 = new Vector3(X(el.x2), Y(el.y1), Z(el.z2));
                var p7 = new Vector3(X(el.x2), Y(el.y2), Z(el.z2));
                var p8 = new Vector3(X(el.x1), Y(el.y2), Z(el.z2));

                var faces = new[]
                {
                    new Face3D(p1, p2, p3), new Face3D(p1, p3, p4),
                    new Face3D(p5, p6, p7), new Face3D(p5, p7, p8),
                    new Face3D(p1, p2, p6), new Face3D(p1, p6, p5),
                    new Face3D(p4, p3, p7), new Face3D(p4, p7, p8),
                    new Face3D(p1, p4, p8), new Face3D(p1, p8, p5),
                    new Face3D(p2, p3, p7), new Face3D(p2, p7, p6),
                };

                var layer = new Layer(el.label) { Color = AciColor.Yellow };
                foreach (var f in faces)
                {
                    if (el.label.Contains("icon"))
                    {
                        var sName = el.additionalInfos.iconName;
                        bool isExhaust = el.additionalInfos.airPath.ToLower() == "exhaust";
                        if (Dxf2DService.IconMap.ContainsKey(sName!))
                        {
                            var insertIcon = iconsList.FirstOrDefault(b => b.Name.Equals(sName, StringComparison.OrdinalIgnoreCase));
                            switch (el.additionalInfos.iconPosition)
                            {
                                case ViewName.Operational:
                                    var insertIconOperational = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(X(el.x1), Y(el.y1), Z(el.z1)), // przesunięcie w bok
                                        Layer = iconLayer,
                                        Scale = new Vector3(1, 1, 1)
                                    };
                                    if (!isExhaust && el.additionalInfos.sName == "VF")
                                    {
                                        insertIconOperational.Position = new Vector3(X(el.x1 + (el.x2 - el.x1)), Y(el.y1), Z(el.z1));
                                        insertIconOperational.Scale = new Vector3(-1, 1, 1);
                                    }

                                    if (el.View == ViewName.Operational)
                                        dxf.Entities.Add(insertIconOperational);
                                    break;

                                case ViewName.Back:
                                    var insertIconBack = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(X(el.x2), Y(el.y1), Z(el.z1)),
                                        Layer = iconLayer,
                                        Scale = new Vector3(-1, 1, 1)
                                    };
                                    if (isExhaust && el.additionalInfos.sName == "VF")
                                    {
                                        insertIconBack.Position = new Vector3(X(el.x1), Y(el.y1), Z(el.z1));
                                        insertIconBack.Scale = new Vector3(1, 1, 1);
                                    }

                                    if (el.View == ViewName.Back)
                                        dxf.Entities.Add(insertIconBack);
                                    break;

                                case ViewName.Up:
                                    var insertIconUp = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(X(el.x1), Y(el.y1), Z(el.z1)),
                                        Layer = iconLayer,
                                    };
                                    if (!isExhaust && el.additionalInfos.sName == "VF")
                                    {
                                        insertIconUp.Position = new Vector3(X(el.x1), Y(el.y1), Z(el.z1));
                                        insertIconUp.Scale = new Vector3(-1, 1, 1);
                                    }

                                    dxf.Entities.Add(insertIconUp);
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                    if (el.label.Contains("icon"))
                    {
                        f.Layer = iconLayer;
                    }
                    else
                    {
                        f.Layer = layer;
                    }                        

                    dxf.Entities.Add(f);
                }
            }

            dxf.Save(filePath);
        }

        public void Generate3D_WorkOK(List<Coordinates> elements, string filePath)
        {
            var dxf = new DxfDocument();
            if (elements == null || elements.Count == 0) return;

            // raz przed foreachem:
            double minX = double.PositiveInfinity, 
                   minY = double.PositiveInfinity, 
                   zFront = double.NegativeInfinity;

            foreach (var e in elements)
            {
                minX = Math.Min(minX, Math.Min(e.x1, e.x2));
                minY = Math.Min(minY, Math.Min(e.y1, e.y2));
                zFront = Math.Max(zFront, Math.Max(e.z1, e.z2)); // jeśli u "front" zawsze = z2 -> Math.Max(zFront, e.z2)
            }

            // skróty (żeby w pętli było czytelnie)
            double X(double x) => x - minX;
            double Y(double y) => y - minY;
            double Z(double z) => zFront - z;

            foreach (var el in elements)
            {
                var p1 = new Vector3(X(el.x1), Y(el.y1), Z(el.z1));
                var p2 = new Vector3(X(el.x2), Y(el.y1), Z(el.z1));
                var p3 = new Vector3(X(el.x2), Y(el.y2), Z(el.z1));
                var p4 = new Vector3(X(el.x1), Y(el.y2), Z(el.z1));

                var p5 = new Vector3(X(el.x1), Y(el.y1), Z(el.z2));
                var p6 = new Vector3(X(el.x2), Y(el.y1), Z(el.z2));
                var p7 = new Vector3(X(el.x2), Y(el.y2), Z(el.z2));
                var p8 = new Vector3(X(el.x1), Y(el.y2), Z(el.z2));

                var faces = new[]
                {
                    new Face3D(p1, p2, p3), new Face3D(p1, p3, p4),
                    new Face3D(p5, p6, p7), new Face3D(p5, p7, p8),
                    new Face3D(p1, p2, p6), new Face3D(p1, p6, p5),
                    new Face3D(p4, p3, p7), new Face3D(p4, p7, p8),
                    new Face3D(p1, p4, p8), new Face3D(p1, p8, p5),
                    new Face3D(p2, p3, p7), new Face3D(p2, p7, p6),
                };

                var layer = new Layer(el.label) { Color = AciColor.Yellow };
                foreach (var f in faces)
                {
                    f.Layer = layer;
                    dxf.Entities.Add(f);
                }
            }

            dxf.Save(filePath);
        }

        public void Generate3D_old(List<Coordinates> elements, string filePath)
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
