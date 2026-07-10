using Klimor.WebApi.DXF.Consts;
using Klimor.WebApi.DXF.Structures;
using netDxf;
using netDxf.Blocks;
using netDxf.Collections;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.DataFormats;

namespace Klimor.WebApi.DXF.Services
{
    public class Dxf3DService
    {
        public ViewsList Views;
        public Norm calculationNorm;
        private string ahuType;

        public Dxf3DService()
        {
            Views = new ViewsList();
        }

        public void Generate3D(List<Coordinates> elements, string filePath, Norm drawingNorm, FirstStepInput input)
        {
            ahuType = input.AhuType;
            Views = new ViewsList();
            var dxf = new DxfDocument();

            Views.AhuLength = elements.Where(el => el.label == Lab.Block).Max(e => e.x2);
            Views.AhuHeight = elements.Where(el => el.label == Lab.Block).Max(e => e.y2);
            Views.AhuWidth = elements.Where(el => el.label == Lab.Block).Max(e => e.z2);

            var blocksLayer = dxf.Layers.Add(new Layer(Lab.Block) { Color = new AciColor(7) });
            var functionsLayer = dxf.Layers.Add(new Layer(Lab.Function) { Color = new AciColor(7), IsVisible = false });
            var operationalLayer = dxf.Layers.Add(new Layer(Lab.Operational) { Color = new AciColor(7) });
            var backLayer = dxf.Layers.Add(new Layer(Lab.Back) { Color = new AciColor(7) });
            var upLayer = dxf.Layers.Add(new Layer(Lab.Up) { Color = new AciColor(7) });
            var downLayer = dxf.Layers.Add(new Layer(Lab.Down) { Color = new AciColor(7) });
            var holeLayer = dxf.Layers.Add(new Layer(Lab.Hole) { Color = new AciColor(7) });
            var airDamperLayer = dxf.Layers.Add(new Layer(Lab.AD) { Color = new AciColor(7) });
            var flexibleConnectionLayer = dxf.Layers.Add(new Layer(Lab.FC) { Color = new AciColor(7) });
            var intakeOutletLayer = dxf.Layers.Add(new Layer(Lab.INTK) { Color = new AciColor(7) });
            var iconLayer = dxf.Layers.Add(new Layer(Lab.Icon) { Color = new AciColor(7) });
            var frameLayer = dxf.Layers.Add(new Layer(Lab.Frame) { Color = new AciColor(7) });
            var portholeLayer = dxf.Layers.Add(new Layer(Lab.Porthole) { Color = new AciColor(7) });

            var icons = DxfDocument.Load("BLOCKS.dxf");
            var iconsList = icons.Blocks.ToList();

            // ikony bez tego nie będą wstawiane
            var mapIcons = elements.Where(e => e.label.Contains("icon")).ToList();
            foreach (var icon in mapIcons)
            {
                icon.View = icon.additionalInfos.iconPosition;
            }

            if (elements == null || elements.Count == 0) return;

            double minX = double.PositiveInfinity,
                   minY = double.PositiveInfinity,
                   minZ = double.PositiveInfinity;

            foreach (var e in elements)
            {
                minX = Math.Min(minX, Math.Min(e.x1, e.x2));
                minY = Math.Min(minY, Math.Min(e.y1, e.y2));
                minZ = Math.Min(minZ, Math.Min(e.z1, e.z2));
            }

            double X(double x) => x - minX;
            double Y(double y) => y - minY;
            double Z(double z) => z - minZ;

            // Dodaj warstwę i tekst "EVO" jako znak wodny
            var textLayer = dxf.Layers.Add(new Layer("WatermarkEVO") { Color = new AciColor(7) });
            var arrowLayer = dxf.Layers.Add(new Layer("Arrows") { Color = new AciColor(7) });

            Add3DWatermark(dxf, textLayer, elements, X, Y, Z, textValue: "EVO", margin: 100);

            // End dodawania znaku wodnego

            foreach (var b in icons.Blocks)
            {
                b.Origin = new Vector3(50, 50, 0);
            }

            static double Normalize360(double deg)
            {
                deg %= 360.0;
                if (deg < 0) deg += 360.0;
                return deg;
            }

            foreach (var el in elements)
            {
                if (el.label == "Porthole")
                {
                    double cx = X((el.x1 + el.x2) / 2.0);
                    double radius;

                    if (el.posUpDown == "Porthole_Down")
                    {
                        double cy = Z((el.z1 + el.z2) / 2.0);
                        double cz = Y(el.y1);

                        radius = Math.Min(
                            Math.Abs(X(el.x2) - X(el.x1)),
                            Math.Abs(Z(el.z2) - Z(el.z1))
                        ) / 2.0;

                        var circle = new Circle(new Vector3(cx, cy, cz), radius)
                        {
                            Layer = portholeLayer,
                            Normal = new Vector3(0, 0, 1)
                        };

                        dxf.Entities.Add(circle);
                    }
                    else
                    {
                        double cy = Z(el.z1);
                        double cz = Y((el.y1 + el.y2) / 2.0);

                        radius = Math.Min(
                            Math.Abs(X(el.x2) - X(el.x1)),
                            Math.Abs(Y(el.y2) - Y(el.y1))
                        ) / 2.0;

                        var circle = new Circle(new Vector3(cx, cy, cz), radius)
                        {
                            Layer = portholeLayer,
                            Normal = new Vector3(0, 1, 0)
                        };

                        dxf.Entities.Add(circle);
                    }

                    continue;
                }

                var p1 = new Vector3(X(el.x1), Z(el.z1), Y(el.y1)); // lewy-dolny-przód
                var p2 = new Vector3(X(el.x2), Z(el.z1), Y(el.y1)); // prawy-dolny-przód
                var p3 = new Vector3(X(el.x2), Z(el.z1), Y(el.y2)); // prawy-górny-przód
                var p4 = new Vector3(X(el.x1), Z(el.z1), Y(el.y2)); // lewy-górny-przód

                var p5 = new Vector3(X(el.x1), Z(el.z2), Y(el.y1)); // lewy-dolny-tył
                var p6 = new Vector3(X(el.x2), Z(el.z2), Y(el.y1)); // prawy-dolny-tył
                var p7 = new Vector3(X(el.x2), Z(el.z2), Y(el.y2)); // prawy-górny-tył
                var p8 = new Vector3(X(el.x1), Z(el.z2), Y(el.y2)); // lewy-górny-tył

                // trójkąty
                //var faces = new[]
                //{
                //    new Face3D(p1, p2, p3), new Face3D(p1, p3, p4),
                //    new Face3D(p5, p6, p7), new Face3D(p5, p7, p8),
                //    new Face3D(p1, p2, p6), new Face3D(p1, p6, p5),
                //    new Face3D(p4, p3, p7), new Face3D(p4, p7, p8),
                //    new Face3D(p1, p4, p8), new Face3D(p1, p8, p5),
                //    new Face3D(p2, p3, p7), new Face3D(p2, p7, p6),
                //};

                var faces = new[]
                {
                    new Face3D(p1, p2, p3, p4), // front
                    new Face3D(p5, p6, p7, p8), // back
                
                    new Face3D(p1, p2, p6, p5), // bottom
                    new Face3D(p4, p3, p7, p8), // top
                
                    new Face3D(p1, p4, p8, p5), // left
                    new Face3D(p2, p3, p7, p6), // right
                };

                var layer = new Layer(el.label) { Color = new AciColor(7) };

                if (el.label == Lab.Hole)
                {
                    Create3DArrow(dxf, arrowLayer, el.additionalInfos?.airPath ?? "", el.additionalInfos?.airPathPosition ?? "", el.additionalInfos?.direction ?? "", el.additionalInfos?.isLeftSide ?? false, X(el.x1), ahuType == AhuTypeName.Evo ? Z(el.y1) : Z(el.z1), el, el.View);
                }
                
                foreach (var f in faces)
                {
                    if (el.label.Contains("icon"))
                    {
                        var sName = el.additionalInfos.iconName;
                        if (sName is ("PFM" or "PFO" or "PFC" or "PFD")) sName = "PF";

                        if (Dxf2DService.IconMap.ContainsKey(sName!))
                        {
                            var insertIcon = iconsList.FirstOrDefault(b => b.Name.Equals(sName, StringComparison.OrdinalIgnoreCase));

                            var cw = el.additionalInfos.iconRotation;
                            var dxfIconRotation = Normalize360(360.0 - cw);

                            switch (el.additionalInfos.iconPosition)
                            {
                                case ViewName.Operational:
                                    var insertIconOperational = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(X(el.x1 + 50), Z(el.z1), Y(el.y1 + 50)),
                                        Layer = iconLayer,
                                        Scale = new Vector3(1, 1, 1),
                                        Normal = new Vector3(0, -1, 0),
                                        Rotation = dxfIconRotation
                                    };

                                    if (el.View == ViewName.Operational)
                                        dxf.Entities.Add(insertIconOperational);

                                    break;

                                case ViewName.Back:
                                    var insertIconBack = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(X(el.x1 + 50), Z(el.z2), Y(el.y1 + 50)),
                                        Layer = iconLayer,
                                        Scale = new Vector3(1, 1, 1),
                                        Normal = new Vector3(0, 1, 0),
                                        Rotation = dxfIconRotation
                                    };

                                    if (el.View == ViewName.Back)
                                        dxf.Entities.Add(insertIconBack);

                                    break;

                                case ViewName.Up:
                                case ViewName.UpUp:
                                    var insertIconUp = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(X(el.x1 + 50), Z(el.z1 + 50), Y(el.y1)),
                                        Layer = iconLayer,
                                        Scale = new Vector3(1, 1, 1),
                                        Normal = new Vector3(0, 0, 1),
                                        Rotation = dxfIconRotation
                                    };

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

        void Create3DArrow(DxfDocument dxf, Layer layer, string airPath, string airPathPosition, string direction, bool isLeftSide, double x, double y, Coordinates el, string viewName)
        {
            var arrowDirection = airPathPosition switch
            {
                "Inlet" when isLeftSide => ArrowDirection.Right,
                "Inlet" when !isLeftSide => ArrowDirection.Left,
                "Outlet" when isLeftSide => ArrowDirection.Left,
                "Outlet" when !isLeftSide => ArrowDirection.Right,
                _ => ArrowDirection.Right
            };

            var label = calculationNorm is (Norm.US or Norm.US_EXTENDED)
                ? airPath switch
                {
                    "Supply" when airPathPosition == "Inlet" => "O/A",
                    "Supply" when airPathPosition == "Outlet" => "S/A",
                    "Exhaust" when airPathPosition == "Inlet" => "R/A",
                    "Exhaust" when airPathPosition == "Outlet" => "C/A",
                    _ => "N/A",
                }
                : airPath switch
                {
                    "Supply" when airPathPosition == "Inlet" => "ODA",
                    "Supply" when airPathPosition == "Outlet" => "SUP",
                    "Exhaust" when airPathPosition == "Inlet" => "ETA",
                    "Exhaust" when airPathPosition == "Outlet" => "EHA",
                    _ => "N/A",
                };

            void AddArrowBlock(double posX, double posY, ArrowDirection dir)
            {
                var arrow = ArrowService.CreateArrow(
                    anchor: new Vector2(0, 0),
                    direction: dir,
                    label: label,
                    arrowSize: 150,
                    padding: 20,
                    outlineColor: airPath == "Supply" ? AciColor.Blue : AciColor.Red,
                    layer: layer,
                    filled: true,
                    textStyle: LabelTextStyles.ArialBold
                );
                arrow.Label.IsBackward = true;

                var block = new Block($"Arrow_{Guid.NewGuid():N}");

                if (arrow.Fill != null)
                    block.Entities.Add(arrow.Fill);

                block.Entities.Add(arrow.Outline);
                block.Entities.Add(arrow.Label);

                dxf.Blocks.Add(block);                

                var isEvot = ahuType == AhuTypeName.Evot;

                var insert = new Insert(block)
                {
                    Position = isEvot
                        ? new Vector3(posX, posY, (el.z2 - el.z1) / 2)
                        : new Vector3(posX, el.y2 - el.y1, posY),

                    Layer = layer,

                    Normal = isEvot
                        ? new Vector3(0, 0, 1)
                        : new Vector3(0, -1, 0),

                    Scale = new Vector3(1, 1, 1)
                };

                dxf.Entities.Add(insert);
            }

            switch (direction)
            {
                case "Front":
                    x = isLeftSide ? x - 450 : x + 450;
                    y += (el.y2 - el.y1) / 2;

                    AddArrowBlock(x, y, arrowDirection);
                    break;

                case "Back":
                    switch (airPath)
                    {
                        case "Supply" when viewName == ViewName.Up:
                            arrowDirection = airPathPosition == "Inlet"
                                ? ArrowDirection.Down
                                : ArrowDirection.Up;

                            x = x + (el.x2 - el.x1) / 2 + 100;
                            y += 250;
                            break;

                        case "Exhaust" when viewName == ViewName.Up:
                            arrowDirection = airPathPosition == "Inlet"
                                ? ArrowDirection.Down
                                : ArrowDirection.Up;

                            x = x + (el.x2 - el.x1) / 2 - 100;
                            y += 250;
                            break;
                    }

                    AddArrowBlock(x, y, arrowDirection);
                    break;
            }
        }

        private void Add3DWatermark(DxfDocument dxf, Layer textLayer, IEnumerable<Coordinates> elements, 
            Func<double, double> X, Func<double, double> Y, Func<double, double> Z, string textValue, double margin = 10)
        {
            var blocks = elements.Where(e => e.label == Lab.Block).ToList();
            if (blocks.Count == 0) return;

            // BBOX po transformacji (tak jak zapisujesz bryły do DXF)
            double xMin = double.PositiveInfinity, xMax = double.NegativeInfinity;
            double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
            double zMin = double.PositiveInfinity, zMax = double.NegativeInfinity;

            foreach (var b in blocks)
            {
                var xs = new[] { X(b.x1), X(b.x2) };
                var ys = new[] { Y(b.y1), Y(b.y2) };
                var zs = new[] { Z(b.z1), Z(b.z2) };

                xMin = Math.Min(xMin, xs.Min()); xMax = Math.Max(xMax, xs.Max());
                yMin = Math.Min(yMin, ys.Min()); yMax = Math.Max(yMax, ys.Max());
                zMin = Math.Min(zMin, zs.Min()); zMax = Math.Max(zMax, zs.Max());
            }

            double midX = (xMin + xMax) / 2.0;

            // "nad obiektem": nad górną krawędzią w osi Y
            double yText = yMax - 55;
            double zText = zMax;

            var vector = ahuType != AhuTypeName.Evo ? 
                new Vector3(midX, zText, yText) : new Vector3(midX, yText, zText);
            var text = new Text(textValue, vector, 40)
            {
                Layer = textLayer,
                Color = new AciColor(7),
                WidthFactor = 1.2,
                Style = LabelTextStyles.ArialBold,
                Rotation = 0,
                Alignment = TextAlignment.BottomCenter,
                Normal = new Vector3(0, 0, 1)
            };

            dxf.Entities.Add(text);
        }

        #region Wcześniejsze wersje (do usunięcia po weryfikacji)
        [Obsolete]
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

        [Obsolete]
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
        #endregion

    }
}
