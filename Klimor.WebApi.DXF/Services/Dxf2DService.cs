using Klimor.WebApi.DXF.Consts;
using Klimor.WebApi.DXF.Development;
using Klimor.WebApi.DXF.Services;
using Klimor.WebApi.DXF.Structures;
using netDxf;
using netDxf.Collections;
using netDxf.Entities;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Klimor.WebApi.DXF.Services
{
    public class Dxf2DService
    {
        ViewsList Views;

        public Dxf2DService(ViewsList vw) 
        {
            Views = vw;
        }

        double profileOffset = 50.0;
        public double globalXMin = 0;
        public double globalXMax = 0;
        public double globalYMin = 0;
        public double globalYMax = 0;
        public double globalZMin = 0;
        public double globalZMax = 0;

        DimensionStyle dimStyle = new DimensionStyle("MyDimStyle")
        {
            TextHeight = 15.0,
            ArrowSize = 15,
            LengthPrecision = 0,
            DimLineColor = AciColor.Yellow,
            ExtLineColor = AciColor.Yellow,
            TextColor = AciColor.Yellow
        };

        public static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>
        {
            { "VF", "fan" },
            { "F", "filter" },
            { "PF", "filter" },
            { "SF", "filter" },
            { "PFD", "filter-double" },
            { "CFS", "chiler" },
            { "CFE", "chiler" },
            { "RG", "glycol" },
            { "CPR", "counterflow-wall" },
            { "DX", "dx" },
            { "ES", "emptysection" },
            { "MX", "mixing" },
            { "PR", "plate" },
            { "RR", "rotor" },
            { "SL", "silencer" },
            { "UV", "uv" },
            { "WC", "watercooler" },
            { "WH", "waterheater" },
            { "EH", "electricheater" },
            { "GM", "gas" },
            { "DE", "dropletEliminator" },
        };

        public void GenerateView(DxfDocument dxf, List<Coordinates> elements, List<string> elementsGroup, bool createDimension, bool createShape, Layer layer, Layer textLayer, IEnumerable<ViewElement> views)
        {
            var firstElement = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
            var lastElement = elements.OrderByDescending(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
            var normTitle = new Text(Views.CurrentNorm.ToString(), new Vector3((lastElement.x2 - firstElement.x1) / 2, 13000, 0), 700)
            {
                Layer = textLayer,
                Rotation = 0,
                Color = AciColor.LightGray,
                WidthFactor = 1.2,
            };
            dxf.Entities.Add(normTitle);

            foreach (var view in views)
            {
                // podpis widoku przy elemencie
                firstElement = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
                lastElement = elements.OrderByDescending(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
                if (firstElement != null)
                {
                    //double elementCenterY = (firstElement.y1 + firstElement.y2) - 500 + view.YOffset;
                    var text = new Text(view.Name, new Vector3(view.XOffset + 200, view.YOffset - 400, 0), 100)
                    {
                        Layer = textLayer,
                        Rotation = 0,
                        Color = AciColor.LightGray,
                        WidthFactor = 1.2,
                    };
                    dxf.Entities.Add(text);
                }

                // wyodrębnienie elementów dla grupy
                var groupElements = elements
                    .Where(e => elementsGroup
                    .Any(g => string.Equals(g, e.label, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                bool externalElementShow = false;
                int externalElementsYOffset = 0;

                // widoki boczne: przycinanie
                if (view.Name == "LeftFront" || view.Name == "RightFront")
                {
                    GenerateSideView(dxf, elements, elementsGroup, createDimension, createShape, layer, view.Name);
                }
                else
                {
                    // dla innych widoków bez przycinania
                    foreach (var el in groupElements)
                    {
                        externalElementShow = false;
                        // generowanie współrzędnych dla widoku
                        List<Vector2> outer2D = GenerateViewVertices(el, view.Name, globalXMin, globalXMax,
                            globalYMin, globalYMax, globalZMin, globalZMax);
                        List<Vector2> inner2D = outer2D.Select(v => new Vector2(v.X + profileOffset, v.Y - profileOffset)).ToList();

                        // przesunięcie Y dla widoku
                        outer2D = outer2D.Select(v => new Vector2(v.X + view.XOffset, v.Y + view.YOffset)).ToList();
                        inner2D = inner2D.Select(v => new Vector2(v.X + view.XOffset, v.Y + view.YOffset)).ToList();

                        // &&*: rysowanie zewnętrznej i wewnętrznej polilinii
                        var outerPoly = new Polyline2D(outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                        {
                            Layer = layer
                        };

                        if (createShape)
                        {
                            if (el.label == Lab.Block)
                            {
                                //if (view.Name == ViewName.Operational)
                                {
                                    AddWatermarkText(dxf, textLayer, elements, view, Views.GetWaterMark());
                                }

                                dxf.Entities.Add(outerPoly); // &&*

                                var left = inner2D.Min(p => p.X);
                                var right = inner2D.Max(p => p.X);
                                var bottom = inner2D.Min(p => p.Y);
                                var top = inner2D.Max(p => p.Y);

                                var bottomLeft = inner2D.First(p => p.X == left && p.Y == bottom);
                                var bottomRight = inner2D.First(p => p.X == right && p.Y == bottom);
                                var topRight = inner2D.First(p => p.X == right && p.Y == top);
                                var topLeft = inner2D.First(p => p.X == left && p.Y == top);

                                // korekta narożników
                                bottomLeft = new Vector2(bottomLeft.X, bottomLeft.Y + 2 * profileOffset);
                                bottomRight = new Vector2(bottomRight.X - 2 * profileOffset, bottomRight.Y + 2 * profileOffset);
                                topRight = new Vector2(topRight.X - 2 * profileOffset, topRight.Y);
                                topLeft = new Vector2(topLeft.X, topLeft.Y);

                                inner2D = new List<Vector2> { bottomLeft, bottomRight, topRight, topLeft };

                                var innerPoly = new Polyline2D(inner2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                                {
                                    Layer = layer
                                };
                                dxf.Entities.Add(innerPoly);

                                var idx = 0;
                                var extra = 20.0;              // długość „wysunięcia” do wnętrza
                                var w = profileOffset;         // szerokość profilu (dotychczasowe 50)

                                foreach (var c in outer2D)
                                {
                                    var cornerVertices = new List<Polyline2DVertex>();

                                    switch (idx)
                                    {
                                        case 0: // lewy dół – rozsunięcie: w prawo (X+) i w górę (Y+)
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w + extra, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w + extra, c.Y + w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w, c.Y + w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w, c.Y + w + extra, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y + w + extra, 0));

                                            if (!view.Name.ToLower().Contains("front") && el.additionalInfos != null)
                                            {
                                                var text = new Text(el.additionalInfos.blockNumber.ToString(),
                                                new Vector3(c.X + 2 * profileOffset, c.Y + 2 * profileOffset, 0), 30);

                                                text.Style = new TextStyle("ArialBold", "arialbd.ttf");
                                                text.Layer = layer;
                                                text.Color = new AciColor(7);
                                                dxf.Entities.Add(text);
                                            }
                                            break;

                                        case 1: // prawy dół – rozsunięcie: w lewo (X−) i w górę (Y+)
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w - extra, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w - extra, c.Y + w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w, c.Y + w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w, c.Y + w + extra, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y + w + extra, 0));
                                            break;

                                        case 2: // prawy góra – rozsunięcie: w lewo (X−) i w dół (Y−)
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w - extra, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w - extra, c.Y - w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w, c.Y - w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - w, c.Y - w - extra, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y - w - extra, 0));
                                            break;

                                        case 3: // lewy góra – rozsunięcie: w prawo (X+) i w dół (Y−)
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w + extra, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w + extra, c.Y - w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w, c.Y - w, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X + w, c.Y - w - extra, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y - w - extra, 0));
                                            break;
                                    }

                                    var cornerPoly = new Polyline2D(cornerVertices, true) { Layer = layer };
                                    var hatch = new Hatch(HatchPattern.Solid, true) { Layer = layer, Color = new AciColor(7) };
                                    hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));

                                    dxf.Entities.Add(hatch);
                                    idx++;
                                }
                                idx = 0;
                            }
                            if (!string.IsNullOrEmpty(el.type) && el.label != Lab.Block)
                            {
                                // dodawanie konektora
                                if ((el.label == Lab.Connector || el.type == Lab.Porthole) && (view.Name == ViewName.Operational || view.Name == ViewName.Back))
                                {
                                    AddCircle(outer2D, dxf, el, layer);
                                    continue;
                                }

                                // dopasowywanie elementów zewnętrznych do widoku                                                               
                                if (Lab.ExternalElements.Any(l => l == el.label))
                                {
                                    // AD, FC na widokach up, down, back, operational
                                    if (el.label != Lab.Hole && Lab.ExternalElements.Any(l => l == el.label))
                                    {
                                        externalElementShow = true;
                                    }
                                    
                                    if (el.label == Lab.Frame && view.Name == Lab.Operational)
                                    {
                                        externalElementShow = true;
                                    }
                                    else if (el.additionalInfos != null)
                                    {
                                        if (el.additionalInfos.direction == "Front" && el.additionalInfos.direction != "Back" && view.Name == Lab.Operational && el.label != Lab.Hole)
                                            externalElementShow = true;
                                        if (el.additionalInfos.direction == "Back" && view.Name == Lab.Back)
                                            externalElementShow = true;
                                        if (el.additionalInfos.direction == "Up" && view.Name == Lab.Back)
                                            externalElementShow = true;
                                    }                                                                       
                                }

                                if (externalElementShow)
                                {
                                    externalElementsYOffset = el.label switch
                                    {
                                        Lab.AD => 30,
                                        Lab.FC => 60,
                                        Lab.INTK => 90,
                                        _ => 0
                                    };
                                }

                                if (el.label == view.Name || el.label == Lab.Function || externalElementShow
                                    || (view.Name == ViewName.Down && el.label.Contains("_")) || el.label == Lab.Frame || el.type == Lab.Switchbox || el.label == Lab.Connector)
                                {
                                    dxf.Entities.Add(outerPoly); // &&*
                                }

                                if ((el.type == "Wall" ||                                    
                                    el.type.Contains("Removable") || 
                                    el.type.Contains("Door")) && el.label == view.Name ||
                                    el.label.Contains("_") || externalElementShow)
                                {
                                    var idx = 0;
                                    foreach (var c in outer2D)
                                    {
                                        var cornerVertices = new List<Polyline2DVertex>();
                                        switch (idx)
                                        {
                                            case 0: // lewy dół
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X + profileOffset, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X + profileOffset, c.Y + profileOffset, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y + profileOffset, 0));
                                                break;

                                            case 1: // prawy dół
                                                cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y + profileOffset, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y + profileOffset, 0));

                                                if (!view.Name.ToLower().Contains("front")
                                                    && el.label == view.Name ||
                                                    externalElementShow // elementy zewnętrzne
                                                    || (el.label.Contains("_") && view.Name == "Down"))
                                                {
                                                    var wallDescription = el.label switch
                                                    {
                                                        "Up" => "UP",
                                                        "Operational" => "INS",
                                                        "Back" => "BACK",
                                                        "Down" => "Down",
                                                        "Down_Wall" => "DOWN",
                                                        "Down_DrainTray" => "DRN_TRY",
                                                        "Frame" => "",
                                                        _ => ""
                                                    };

                                                    if (wallDescription == "INS")
                                                    {
                                                        wallDescription = el.type switch
                                                        {
                                                            "Door" => "DOOR",
                                                            "Removable" => "PNL_GRIP",
                                                            "Removable_2" => "PNL_HH",
                                                            "Removable_3" => "PNL_BSH",
                                                            "Wall" => "PNL", //operational, back, frontLeft, frontRight, up, down, middle
                                                            "DrainTray" => "DRN_TY", //down, middle
                                                            "Hole" => "HOLE", //operational, back, frontLeft, frontRight, up, down, middle
                                                            "Div" => "", //operational, back, frontLeft, frontRight, up, down, middle  
                                                            _ => "INS"
                                                        };
                                                    }
                                                    var text = new Text(wallDescription,
                                                        new Vector3(c.X - ((el.x2 - el.x1) / 2) - profileOffset, c.Y + 4 * profileOffset + externalElementsYOffset, 0), 20);

                                                    text.Style = new TextStyle("ArialBold", "arialbd.ttf");
                                                    text.Layer = layer;
                                                    text.Color = new AciColor(3);
                                                    dxf.Entities.Add(text);
                                                }
                                                break;

                                            case 2: // prawy góra
                                                cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y - profileOffset, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y - profileOffset, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y, 0));
                                                break;

                                            case 3: // lewy góra
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X + profileOffset, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X + profileOffset, c.Y - profileOffset, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y - profileOffset, 0));
                                                break;
                                        }

                                        idx++;
                                    }
                                    idx = 0;
                                }
                            }
                        }

                        if (createDimension)
                        {
                            double dimOffset = 30.0;
                            var wStart = outer2D[0];
                            var wEnd = outer2D[1];
                            var widthDim = new LinearDimension(wStart, wEnd, -dimOffset, 0.0, dimStyle)
                            {
                                Layer = layer
                            };

                            if (!string.IsNullOrEmpty(el.type))
                            {
                                // elementy zewnętrzne
                                if (externalElementShow)
                                {
                                    widthDim = new LinearDimension(wStart, wEnd, (el.y2 - el.y1) / 2, 0.0, dimStyle);
                                }

                                // widok operational
                                if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Operational && view.Name == ViewName.Operational)
                                {
                                    widthDim = new LinearDimension(wStart, wEnd, (el.y2 - el.y1) / 2 - profileOffset, 0.0, dimStyle);
                                }

                                // widok back
                                if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable) || el.label == Lab.Frame) && el.label == Lab.Back && view.Name == ViewName.Back)
                                {
                                    widthDim = new LinearDimension(wStart, wEnd, (el.y2 - el.y1) / 2, 0.0, dimStyle);
                                }

                                // widok up
                                if (el.type == Lab.Wall && el.label == Lab.Up && view.Name == ViewName.Up)
                                {
                                    widthDim = new LinearDimension(wStart, wEnd, (el.z2 - el.z1) / 2, 0.0, dimStyle);
                                }

                                // widok down
                                if ((el.label == Lab.Down_Wall || el.label == Lab.Down_DrainTray) && view.Name == ViewName.Down)
                                {
                                    widthDim = new LinearDimension(wStart, wEnd, (el.z2 - el.z1) / 2, 0.0, dimStyle);
                                    widthDim.Layer = layer;
                                    dxf.Entities.Add(widthDim);
                                }
                            }

                            if (el.label == view.Name || el.label == Lab.Function || el.label == Lab.Block || Lab.ExternalElements.Any(l => l == el.label))
                            {
                                widthDim.Layer = layer;
                                dxf.Entities.Add(widthDim);
                            }

                            var hStart = outer2D[1];
                            var hEnd = outer2D[2];
                            var heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle)
                            {
                                Layer = layer
                            };

                            if (!string.IsNullOrEmpty(el.type))
                            {
                                // elementy zewnętrzne
                                if (externalElementShow)
                                {
                                    heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle);
                                }

                                // widok operational
                                if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Operational && view.Name == ViewName.Operational)
                                {
                                    heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle);
                                }

                                // widok back
                                if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Back && view.Name == ViewName.Back)
                                {
                                    heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle);
                                }

                                // widok up
                                if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Up && view.Name == ViewName.Up)
                                {
                                    heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle);
                                }

                                // widok down
                                if ((el.label == Lab.Down || el.label == Lab.Down_DrainTray || el.label == Lab.Down_Wall) && view.Name == Lab.Down)
                                {
                                    heightDim.Layer = layer;
                                    dxf.Entities.Add(heightDim);
                                    heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle);
                                }
                            }

                            if (el.label == view.Name || el.label == Lab.Function || el.label == Lab.Block || Lab.ExternalElements.Any(l => l == el.label))
                            {
                                heightDim.Layer = layer;
                                dxf.Entities.Add(heightDim);
                            }
                        }
                    }
                }
            }
        }

        void AddCircle(List<Vector2> outer2D, DxfDocument dxf, Coordinates el, Layer layer)
        {            
            // obwiednia kwadratu (outer2D ma 4 narożniki)
            double minX = outer2D.Min(p => p.X);
            double maxX = outer2D.Max(p => p.X);
            double minY = outer2D.Min(p => p.Y);
            double maxY = outer2D.Max(p => p.Y);

            // środek kwadratu
            var center = new Vector3((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0.0);

            // promień = połowa boku (ew mniejszy wymiar)
            double radius = Math.Min(maxX - minX, maxY - minY) / 2.0;

            var circle = new Circle(center, radius)
            {
                Layer = layer
            };
            dxf.Entities.Add(circle);
            
            var text = new Text(el.label, new Vector3(center.X + profileOffset, center.Y - 10, 0), 20) { Layer = layer };
            dxf.Entities.Add(text);
        }

        public double ReflectZ(double z) => globalZMax + globalZMin - z;

        public void GetProjectedZ(Coordinates el, string viewName, out double zLeft, out double zRight)
        {
            if (viewName == "LeftFront") // tu robimy odbicie
            {
                double a = ReflectZ(el.z2);
                double b = ReflectZ(el.z1);
                zLeft = Math.Min(a, b);
                zRight = Math.Max(a, b);
            }
            else // rightFront – naturalne
            {
                zLeft = el.z1;
                zRight = el.z2;
            }
        }

        public double FrontDepth(Coordinates el, string viewName)
        {
            if (viewName == "LeftFront")
                // przód = odbity z2
                return ReflectZ(el.z2);
            else
                // rightFront przód = z1
                return el.z1;
        }

        public void GenerateSideView(DxfDocument dxf, List<Coordinates> elements, List<string> elementsGroup, bool createDimension, bool createShape, Layer layer, string viewName)
        {
            var groupElements = elements
                .Where(e => elementsGroup.Any(g => string.Equals(e.label, g, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var sideElements = groupElements
                .OrderBy(e => FrontDepth(e, viewName))
                .ToList();

            List<Rect2D> visibleRects = new List<Rect2D>();

            // do AD/FC
            var blocksAll = elements.Where(e => e.label == "Block").OrderBy(e => e.x1).ToList();
            double secondBlockX1 = blocksAll.Skip(1).FirstOrDefault()?.x1 ?? double.MaxValue;
            double secondLastBlockX2 = blocksAll.OrderByDescending(e => e.x2).Skip(1).FirstOrDefault()?.x2 ?? double.MinValue;

            //var yOffset = views.FirstOrDefault(v => v.name == viewName).yOffset;
            var yOffset = Views[viewName].YOffset;
            var xOffset = Views[viewName].XOffset;

            // pełne kontury bloków (profil)
            if (createShape && elementsGroup.Any(g => string.Equals(g, "Block", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var block in sideElements.Where(e => e.label == "Block"))
                {
                    GetProjectedZ(block, viewName, out double zL, out double zR);

                    var rectFull = new Rect2D(block.y1, block.y2, zL, zR, "Block");

                    var outer2DFull = new List<Vector2>
                        {
                            new Vector2(rectFull.Z1 + xOffset, rectFull.Y1 + yOffset),
                            new Vector2(rectFull.Z2 + xOffset, rectFull.Y1 + yOffset),
                            new Vector2(rectFull.Z2 + xOffset, rectFull.Y2 + yOffset),
                            new Vector2(rectFull.Z1 + xOffset, rectFull.Y2 + yOffset)
                        };

                    var profile = new Polyline2D(
                        outer2DFull.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(),
                        true
                    )
                    {
                        Layer = layer
                    };

                    AddWatermarkText(dxf, layer, elements, Views[viewName], Views.GetWaterMark());
                    dxf.Entities.Add(profile);
                }
            }

            // wyliczanie widocznych fragmentów (Subtract) + rysowanie
            foreach (var el in sideElements)
            {
                if ((el.label == "AD" || el.label == "FC") && viewName == "LeftFront" && el.x1 >= secondBlockX1) continue;
                if ((el.label == "AD" || el.label == "FC") && viewName == "RightFront" && el.x2 <= secondLastBlockX2) continue;

                GetProjectedZ(el, viewName, out double zLeft, out double zRight);

                var rect = new Rect2D(el.y1, el.y2, zLeft, zRight, el.label);

                List<Rect2D> toAdd = new List<Rect2D> { rect };
                foreach (var existing in visibleRects)
                {
                    var next = new List<Rect2D>();
                    foreach (var r in toAdd)
                        next.AddRange(r.Subtract(existing));
                    toAdd = next;
                    if (toAdd.Count == 0) break;
                }

                visibleRects.AddRange(toAdd);
            }

            // rysowanie widocznych fragmentów
            foreach (var r in visibleRects)
            {
                var outer2D = r.ToVertices().Select(v => new Vector2(v.X + xOffset, v.Y + yOffset)).ToList();

                if (createShape)
                {
                    var outerPoly = new Polyline2D(
                        outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(),
                        true
                    )
                    {
                        Layer = layer
                    };
                    dxf.Entities.Add(outerPoly);

                    if (r.SourceLabel == "Block")
                    {
                        double profileThickness = 50.0;

                        // wewnętrzna linia
                        double iZ1 = r.Z1 + profileThickness;
                        double iZ2 = r.Z2 - profileThickness;
                        double iY1 = r.Y1 + profileThickness;
                        double iY2 = r.Y2 - profileThickness;

                        if (iZ2 > iZ1 && iY2 > iY1)
                        {
                            var inner2D = new List<Vector2>
                                {
                                    new Vector2(iZ1 + xOffset, iY1 + yOffset),
                                    new Vector2(iZ2 + xOffset, iY1 + yOffset),
                                    new Vector2(iZ2 + xOffset, iY2 + yOffset),
                                    new Vector2(iZ1 + xOffset, iY2 + yOffset)
                                };

                            var innerPoly = new Polyline2D(
                                inner2D.Select(p => new Polyline2DVertex(p.X, p.Y, 0)).ToList(),
                                true
                            )
                            {
                                Layer = layer
                            };
                            dxf.Entities.Add(innerPoly);
                        }

                        // narożniki do środka
                        for (int i = 0; i < outer2D.Count; i++)
                        {
                            var c = outer2D[i];
                            double dx = 0, dy = 0;
                            switch (i)
                            {
                                case 0: dx = profileThickness; dy = profileThickness; break;
                                case 1: dx = -profileThickness; dy = profileThickness; break;
                                case 2: dx = -profileThickness; dy = -profileThickness; break;
                                case 3: dx = profileThickness; dy = -profileThickness; break;
                            }

                            var cornerVertices = new List<Polyline2DVertex>
                                {
                                    new Polyline2DVertex(c.X,       c.Y,       0),
                                    new Polyline2DVertex(c.X + dx,  c.Y,       0),
                                    new Polyline2DVertex(c.X + dx,  c.Y + dy,  0),
                                    new Polyline2DVertex(c.X,       c.Y + dy,  0)
                                };

                            var cornerPoly = new Polyline2D(cornerVertices, true) { Layer = layer };
                            var hatch = new Hatch(HatchPattern.Solid, true);
                            hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));
                            hatch.Layer = layer;
                            hatch.Color = new AciColor(7);
                            dxf.Entities.Add(hatch);
                        }
                    }
                }

                if (createDimension)
                {
                    double dimOffset = 30.0;
                    var wStart = outer2D[0];
                    var wEnd = outer2D[1];
                    dxf.Entities.Add(new LinearDimension(wStart, wEnd, -dimOffset, 0.0, dimStyle) { Layer = layer });

                    var hStart = outer2D[1];
                    var hEnd = outer2D[2];
                    dxf.Entities.Add(new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle) { Layer = layer });
                }
            }
        }

        // współrzędne dla poszczególnych widoków / perspektyw
        public List<Vector2> GenerateViewVertices(Coordinates el, string view, double globalXMin, double globalXMax,
            double globalYMin, double globalYMax, double globalZMin, double globalZMax)
        {
            double x1 = el.x1, x2 = el.x2;
            double y1 = el.y1, y2 = el.y2;
            double z1 = el.z1, z2 = el.z2;

            switch (view)
            {
                case "Operational": // bazowy XY
                    return new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y1), new Vector2(x2, y2), new Vector2(x1, y2) };

                case "Back": // odbicie względem osi X całego zestawu
                    double newX1 = globalXMax + globalXMin - x1;
                    double newX2 = globalXMax + globalXMin - x2;
                    return new List<Vector2> { new Vector2(newX2, y1), new Vector2(newX1, y1), new Vector2(newX1, y2), new Vector2(newX2, y2) };

                case "Up": // widok z góry (XZ)                    
                    return new List<Vector2> { new Vector2(x1, z1), new Vector2(x2, z1), new Vector2(x2, z2), new Vector2(x1, z2) };

                case "Down": // widok z dołu (XZ, odbicie w Z)
                    double newZ1Down = globalZMax + globalZMin - z1;
                    double newZ2Down = globalZMax + globalZMin - z2;
                    return new List<Vector2> { new Vector2(x1, newZ2Down), new Vector2(x2, newZ2Down), new Vector2(x2, newZ1Down), new Vector2(x1, newZ1Down) };

                case "LeftFront": // widok z lewej (YZ)
                    return new List<Vector2> { new Vector2(z1, y1), new Vector2(z2, y1), new Vector2(z2, y2), new Vector2(z1, y2) };

                case "RightFront": // odbicie w Z
                    double newZ1 = globalZMax + globalZMin - z1;
                    double newZ2 = globalZMax + globalZMin - z2;
                    return new List<Vector2> { new Vector2(newZ2, y1), new Vector2(newZ1, y1), new Vector2(newZ1, y2), new Vector2(newZ2, y2) };

                case "Frame": // widok z dołu (XZ, odbicie w Z) - jak dla Down-a
                    double frameZ1Down = globalZMax + globalZMin - z1;
                    double frameZ2Down = globalZMax + globalZMin - z2;
                    return new List<Vector2> { new Vector2(x1, frameZ2Down), new Vector2(x2, frameZ2Down), new Vector2(x2, frameZ1Down), new Vector2(x1, frameZ1Down) };

                case "Roof": // widok z dołu (XZ, odbicie w Z) - jak dla Down-a
                    return new List<Vector2> { new Vector2(x1, z1), new Vector2(x2, z1), new Vector2(x2, z2), new Vector2(x1, z2) };

                default:
                    return new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y1), new Vector2(x2, y2), new Vector2(x1, y2) };
            }
        }

        // Wstawia tekst na środku całej jednostki (po X) i "w profilu" (na górnym profilu – w połowie jego grubości)
        private void AddWatermarkText(
            DxfDocument dxf,
            Layer textLayer,
            IEnumerable<Coordinates> allElements,
            ViewElement view,
            string textValue,
            double textHeight = 35)
        {
            var blocks = allElements.Where(e => e.label == Lab.Block).ToList();
            if (blocks.Count == 0) return;

            var xMin = blocks.Min(b => b.x1);
            var xMax = blocks.Max(b => b.x2);
            var yMin = blocks.Min(b => b.y1);
            var yMax = blocks.Max(b => b.y2);
            var zMin = blocks.Min(b => b.z1);
            var zMax = blocks.Max(b => b.z2);

            var assembly = new Coordinates
            {
                x1 = xMin,
                x2 = xMax,
                y1 = yMin,
                y2 = yMax,
                z1 = zMin,
                z2 = zMax,
                label = Lab.Block
            };

            var rect2D = GenerateViewVertices(assembly, view.Name, globalXMin, globalXMax, globalYMin, globalYMax, globalZMin, globalZMax);

            // offset widoku
            rect2D = rect2D.Select(p => new Vector2(p.X + view.XOffset, p.Y + view.YOffset)).ToList();

            // wyliczamy środek po X oraz „górę” prostokąta po Y,
            // a następnie schodzimy o połowę grubości profilu
            double leftX = rect2D.Min(p => p.X);
            double rightX = rect2D.Max(p => p.X);
            double topY = rect2D.Max(p => p.Y);

            double midX = (leftX + rightX) / 2.0;
            double yInProfile = topY - (profileOffset / 2.0);

            var text = new Text(textValue, new Vector3(midX - 100, yInProfile - 17, 0), textHeight)
            {
                Layer = textLayer,
                Color = AciColor.LightGray,
                Rotation = 0,
                WidthFactor = 1.2,
                Style = new TextStyle("ArialBold", "arialbd.ttf")
            };

            dxf.Entities.Add(text);
        }

        public class Rect2D
        {
            public double Y1, Y2, Z1, Z2;
            public string SourceLabel { get; set; } // skąd powstał fragment (np. "Block")

            public Rect2D(double y1, double y2, double z1, double z2, string sourceLabel = null)
            {
                Y1 = y1; Y2 = y2; Z1 = z1; Z2 = z2;
                SourceLabel = sourceLabel;
            }

            public List<Rect2D> Subtract(Rect2D other)
            {
                var result = new List<Rect2D>();

                double yOverlapMin = Math.Max(Y1, other.Y1);
                double yOverlapMax = Math.Min(Y2, other.Y2);
                double zOverlapMin = Math.Max(Z1, other.Z1);
                double zOverlapMax = Math.Min(Z2, other.Z2);

                // brak nakładania - cały prostokąt zostaje
                if (yOverlapMax <= yOverlapMin || zOverlapMax <= zOverlapMin)
                {
                    result.Add(this);
                    return result;
                }

                // części nad/pod
                if (Y1 < yOverlapMin)
                    result.Add(new Rect2D(Y1, yOverlapMin, Z1, Z2, SourceLabel));
                if (Y2 > yOverlapMax)
                    result.Add(new Rect2D(yOverlapMax, Y2, Z1, Z2, SourceLabel));

                // części w pasie Y – przycinamy w Z
                if (Z1 < zOverlapMin)
                    result.Add(new Rect2D(yOverlapMin, yOverlapMax, Z1, zOverlapMin, SourceLabel));
                if (Z2 > zOverlapMax)
                    result.Add(new Rect2D(yOverlapMin, yOverlapMax, zOverlapMax, Z2, SourceLabel));

                return result;
            }

            public List<Vector2> ToVertices()
            {
                // kolejność: LL, LR, UR, UL (po Z w poziomie, Y w pionie)
                return new List<Vector2>
                {
                    new Vector2(Z1, Y1),
                    new Vector2(Z2, Y1),
                    new Vector2(Z2, Y2),
                    new Vector2(Z1, Y2)
                };
            }
        }
    }
}
