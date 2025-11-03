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
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static netDxf.Entities.HatchBoundaryPath;

namespace Klimor.WebApi.DXF.Services
{
    /*
        DXF API 2025
        ----------------------------------

        => bloki z narożnikami i wymiarami
        => funkcje z ikonami i wymiarami z BLOCKS.dxf
        => dachy
        => ramy / ramy up, osobny view
        => Hole, AD, FC, INTK
        => DT Conny z okrągłymi na operationalu, a normalnymi na innych
        => wycinanie left/right
        => rzutowanie z perspektywami ISO/ANSI(US) + dynamiczne rozstawianie do przetestowania jak będzie to się sprawdzać
        => switchboxy
        => wallsy z nazewnictwem
        => watermark
        => odwracanie ikony wenta i przesuwanie
        => portholes
        => podzialy basic/advanced
        => DownUp/UpUp na osobnych rzutach
    */

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
        public bool isExtended = true;

        int channelNumberTextSize = 200;

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

        public void GenerateChannelNumbers(List<Coordinates> elements, DxfDocument dxf, ViewElement view, Layer textLayer)
        {
            var upChannel = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block && e.y1 > 120);
            var downChannel = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block && e.y1 <= 120);
            var upUpChannel = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.View == ViewName.UpUp);
            var downUpChannel = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.View == ViewName.DownUp);
            var frameUpChannel = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.View is ViewName.FrameUp);
            var roofUpChannel = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.View == ViewName.RoofUp);

            if (upChannel != null && (view.Name == ViewName.Operational || view.Name == ViewName.Back))
            {
                var numberUp = new Text("2", new Vector3(view.XOffset - 500, view.YOffset + upChannel.y2 - (upChannel.y2 - upChannel.y1) / 2 - 300 / 2, 0), channelNumberTextSize)
                {
                    Layer = textLayer,
                    Rotation = 0,
                    Color = AciColor.LightGray,
                    WidthFactor = 1.2,
                    Style = new TextStyle("ArialBold", "arialbd.ttf")
                };
                dxf.Entities.Add(numberUp);                
            }

            if (downChannel != null && (view.Name == ViewName.Operational || view.Name == ViewName.Back 
                || view.Name == ViewName.Up || view.Name == ViewName.Down || view.Name == ViewName.Frame || view.Name == ViewName.Roof))
            {
                var numberDown = new Text("1", new Vector3(view.XOffset - 500, view.YOffset + downChannel.y2 - (downChannel.y2 - downChannel.y1) / 2 - 300 / 2, 0), channelNumberTextSize)
                {
                    Layer = textLayer,
                    Rotation = 0,
                    Color = AciColor.DarkGray,
                    WidthFactor = 1.2,
                    Style = new TextStyle("ArialBold", "arialbd.ttf")
                };
                dxf.Entities.Add(numberDown);
            }

            if (view.Name == ViewName.UpUp && upUpChannel != null)
            {
                var x1 = upUpChannel.x1 - 600;
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), channelNumberTextSize)
                {
                    Layer = textLayer,
                    Rotation = 0,
                    Color = AciColor.LightGray,
                    WidthFactor = 1.2,
                    Style = new TextStyle("ArialBold", "arialbd.ttf")
                };
                dxf.Entities.Add(numberUp);
            }

            if (view.Name == ViewName.DownUp && downUpChannel != null)
            {
                var x1 = downUpChannel.x1 - 600;
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), channelNumberTextSize)
                {
                    Layer = textLayer,
                    Rotation = 0,
                    Color = AciColor.LightGray,
                    WidthFactor = 1.2,
                    Style = new TextStyle("ArialBold", "arialbd.ttf")
                };
                dxf.Entities.Add(numberUp);
            }


            if (view.Name == ViewName.FrameUp && frameUpChannel != null)
            {
                var x1 = frameUpChannel.x1 - 600;
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), channelNumberTextSize)
                {
                    Layer = textLayer,
                    Rotation = 0,
                    Color = AciColor.LightGray,
                    WidthFactor = 1.2,
                    Style = new TextStyle("ArialBold", "arialbd.ttf")
                };
                dxf.Entities.Add(numberUp);
            }

            if (view.Name == ViewName.RoofUp && roofUpChannel != null)
            {
                var x1 = roofUpChannel.x1 - 600;
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), channelNumberTextSize)
                {
                    Layer = textLayer,
                    Rotation = 0,
                    Color = AciColor.LightGray,
                    WidthFactor = 1.2,
                    Style = new TextStyle("ArialBold", "arialbd.ttf")
                };
                dxf.Entities.Add(numberUp);
            }
        }

        private static void AddSolidFill(
            DxfDocument dxf,
            Layer layer,
            EntityObject outerPoly,
            int index,
            EntityObject? innerPoly = null,
            AciColor? color = null)
        {
            if (dxf == null || outerPoly == null)
                return;
            if (index > 254) index = 1;
            var fill = new Hatch(HatchPattern.Solid, false)
            {
                Layer = layer,
                Color = color ?? new AciColor(0, 0, 0) //new AciColor((byte)(30 + index), (byte)(30 + index), (byte)(30 + index))
            };

            // zewnętrzna granica
            fill.BoundaryPaths.Add(
                new HatchBoundaryPath(new List<EntityObject> { (EntityObject)outerPoly.Clone() })
            );

            // opcjonalna wewnętrzna (otwór)
            if (innerPoly != null)
            {
                fill.BoundaryPaths.Add(
                    new HatchBoundaryPath(new List<EntityObject> { (EntityObject)innerPoly.Clone() })
                );
            }

            dxf.Entities.Add(fill);
        }

        public void GenerateView(DxfDocument dxf, List<Coordinates> elements, List<string> elementsGroup, bool createDimension, bool createShape, Layer layer, Layer textLayer, IEnumerable<ViewElement> views)
        {
            var firstElement = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
            var lastElement = elements.OrderByDescending(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
            var normTitle = new Text(Views.CurrentNorm.ToString(), new Vector3((lastElement.x2 - firstElement.x1) / 2, 30000, 0), 700)
            {
                Layer = textLayer,
                Rotation = 0,
                Color = AciColor.LightGray,
                WidthFactor = 1.2,
            };
            dxf.Entities.Add(normTitle);

            foreach (var view in views.Where(v => v.Visibility))
            {
                // podpis widoku przy elemencie
                firstElement = elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
                lastElement = elements.OrderByDescending(e => e.x1).FirstOrDefault(e => e.label == Lab.Block);
                if (firstElement != null)
                {
                    //double elementCenterY = (firstElement.y1 + firstElement.y2) - 500 + view.YOffset;
                    var textToShow = view.Name switch
                    {
                        ViewName.Operational => "Obsługa/Inspection",
                        ViewName.Back => "Plecy/Back",
                        ViewName.LeftFront => "Lewy bok/Side L",
                        ViewName.RightFront => "Prawy bok/Side R",
                        ViewName.Up => "Sufit/Up",
                        ViewName.UpUp => "Sufit 2/Up2",
                        ViewName.Down => "Podłoga/Down",
                        ViewName.DownUp => "Podłoga/Down2",
                        ViewName.Frame => "Rama",
                        ViewName.FrameUp => "Rama/Frame2",
                        ViewName.Roof => "Dach/Roof",
                        ViewName.RoofUp => "Dach/Roof2",
                        _ => view.Name
                    };

                    var text = new Text(textToShow, new Vector3(view.XOffset, view.YOffset - globalYMax/4, 0), globalXMax*2 / 100)
                    {
                        Layer = textLayer,
                        Rotation = 0,
                        Color = AciColor.LightGray,
                        WidthFactor = 1.2,
                    };
                    dxf.Entities.Add(text);
                }

                // numery kanałów
                if (isExtended)
                {
                    switch (view.Name)
                    {
                        case ViewName.Operational:
                        case ViewName.Back:
                        case ViewName.Up:
                        case ViewName.UpUp:
                        case ViewName.Down:
                        case ViewName.DownUp:
                        //case ViewName.Frame:
                        //case ViewName.FrameUp:
                        case ViewName.Roof:
                        case ViewName.RoofUp:
                            GenerateChannelNumbers(elements, dxf, view, textLayer);
                            break;
                    }
                }                

                // wyodrębnienie elementów dla grupy
                var groupElements = elements
                    .Where(e => elementsGroup
                    .Any(g => string.Equals(g, e.label, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                bool externalElementShow = false;
                int externalElementsYOffset = 0;

                if (view.Name == ViewName.RightFront)
                {
                    groupElements = groupElements.OrderByDescending(e => e.x2).ToList();
                }

                int fillIndexColor = 0;
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
                        if (el.label == Lab.Block && el.View == view.Name && el.Show)
                        {
                            fillIndexColor += 50;
                            AddSolidFill(dxf, layer, outerPoly, fillIndexColor);
                            //if (view.Name == ViewName.Operational)
                            {
                                AddWatermarkText(dxf, textLayer, elements, view, Views.GetWaterMark());
                            }

                            outerPoly.Layer.Color = new AciColor(7);
                            outerPoly.Lineweight = Lineweight.W100;
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
                                            new Vector3(c.X + 2 * profileOffset, c.Y + 2 * profileOffset, 0), 70);

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

                        if ((!string.IsNullOrEmpty(el.type) && el.label != Lab.Block && el.Show) &&
                            (el.View == view.Name || el.label == Lab.Hole) ||
                            (el.label == Lab.Connector && view.Name is (ViewName.LeftFront or ViewName.RightFront)) ||
                            (el.label is (ViewName.LeftFront or ViewName.RightFront)))
                        {
                            // zakrywanie elementów na frontach
                            if (view.Name is (ViewName.LeftFront or ViewName.RightFront))
                            {
                                if (Lab.ExternalElements.Any(l => l == el.label) && (el.View == view.Name))
                                {
                                    var firstblock = view.Name is ViewName.LeftFront ?
                                        elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block) :
                                        elements.OrderBy(e => e.x2).FirstOrDefault(e => e.label == Lab.Block);

                                    if (el.label is (Lab.AD or Lab.FC or Lab.Connector))
                                    {
                                        if (view.Name is ViewName.LeftFront)
                                        {
                                            if (!((el.x1 < firstblock.x1 && el.x1 < firstblock.x2) || (el.x1 < firstblock.x2 && el.y1 > firstblock.y1)))
                                            {
                                                continue;
                                            }
                                        }
                                        else
                                        {
                                            if (!((el.x2 > firstblock.x2 && el.x2 > firstblock.x1) || (el.x2 > firstblock.x1 && el.y1 > firstblock.y1)))
                                            {
                                                continue;
                                            }
                                        }
                                    }
                                }
                            }

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
                                if (Lab.ExternalElements.Any(l => l == el.label) && (el.View == view.Name) ||
                                    (el.label == Lab.Hole && view.Name is (ViewName.Operational or ViewName.Back)) ||
                                    (el.label == Lab.Connector && el.View is (ViewName.LeftFront or ViewName.RightFront)))
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

                            // przesunięcie dla elementów zewnętrznych w Y, żeby się nie nakładały
                            if (externalElementShow)
                            {
                                if (view.Name == ViewName.Operational || view.Name == ViewName.Back || view.Name == ViewName.LeftFront || view.Name == ViewName.RightFront)
                                {
                                    externalElementsYOffset = el.label switch
                                    {
                                        Lab.AD => 30,
                                        Lab.FC => 60,
                                        Lab.INTK => 90,
                                        _ => 0
                                    };
                                }
                                // Up/Down/UpUp/DownUp
                                else
                                {
                                    externalElementsYOffset = -170;
                                }
                            }

                            var isFront = el.View is (ViewName.LeftFront or ViewName.RightFront) ||
                                (view.Name is (ViewName.LeftFront or ViewName.RightFront));

                            if (el.label == view.Name ||
                               (view.Name is (ViewName.Down or ViewName.DownUp) && el.label is (Lab.Down_Div or Lab.Down_DrainTray or Lab.Down_Wall)) ||
                               (view.Name is (ViewName.Up) && el.label is Lab.Wall) ||
                               (view.Name is (ViewName.UpUp) && el.label is Lab.Up) ||
                               (externalElementShow && el.View == view.Name) ||
                               (el.label == Lab.Connector && isFront) ||
                               (el.type == Lab.Wall && isFront && view.Name is not (ViewName.Up or ViewName.UpUp or ViewName.DownUp or ViewName.FrameUp or ViewName.RoofUp))
                               )
                            {
                                dxf.Entities.Add(outerPoly); // &&*
                            }

                            // dodajemy kwadraciki - Up/Down ożebrowanie / znaczniki płyt na Up/Down
                            if (!externalElementShow
                                && (el.type == "Wall" || el.type.Contains("Removable"))
                                && (view.Name == "Up" || view.Name == "Down")
                                && (el.label == "Operational" || el.label == "Back"))
                            {
                                if (el.x2 + 50 < Views.AhuLength)
                                {
                                    var cornerService = new CornerService(dxf, layer);
                                    cornerService.AddFilledCorner(
                                        outer2D[1].X + 50,
                                        outer2D[1].Y,
                                        size: 50,
                                        anchor: AnchorPos.BottomRight
                                    );
                                }
                            }

                            if ((el.type == "Wall" || el.type == "DrainTray" || el.type.Contains("Removable") || el.type.Contains("Door"))
                                || el.label.Contains("_") || externalElementShow)
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

                                            if (el.type == "Wall" || el.type.Contains("Removable") || el.type.Contains("Door")
                                                || externalElementShow // elementy zewnętrzne
                                                || (el.label.Contains("_") && view.Name == "Down"))
                                            {
                                                var wallDescription = el.label switch
                                                {
                                                    "Up" => "UP",
                                                    "UpUp" => "UP",
                                                    "Operational" => "INS",
                                                    "Back" => "BACK",
                                                    "Down" => "Down",
                                                    "DownUp" => "Down",
                                                    "Down_Wall" => "DOWN",
                                                    "Down_DrainTray" => "DRN_TRY",
                                                    "Frame" => "",
                                                    _ => el.label
                                                };

                                                // nadpisanie przesuniętych Down, które jako label mają ustawione DownUp, ale trzymają typ
                                                if (el.type == "DrainTray" && el.label == ViewName.DownUp)
                                                {
                                                    wallDescription = "DRN_TRY";
                                                }

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

                                                if (el.label == view.Name ||
                                                    Lab.ExternalElements.Any(l => l == el.label) ||
                                                    el.label == Lab.Hole && string.IsNullOrEmpty(el.View) ||
                                                    el.View is (ViewName.Down or ViewName.DownUp or ViewName.Up or ViewName.UpUp))
                                                {
                                                    var text = new Text(wallDescription,
                                                    new Vector3(c.X - ((el.x2 - el.x1) / 2) - profileOffset, c.Y + 4 * profileOffset + externalElementsYOffset, 0), 20);

                                                    text.Style = new TextStyle("ArialBold", "arialbd.ttf");
                                                    text.Layer = layer;
                                                    text.Color = new AciColor(3);
                                                    dxf.Entities.Add(text);
                                                }
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

                    if (createDimension && el.ShowDimension)
                    {
                        bool addDim = false;
                        double dimOffset = 30.0;
                        var wStart = outer2D[0];
                        var wEnd = outer2D[1];
                        var widthDim = new LinearDimension(wStart, wEnd, -dimOffset, 0.0, dimStyle)
                        {
                            Layer = layer
                        };

                        var notForBlock = el.label != Lab.Block && el.View != ViewName.Frame;

                        if (!string.IsNullOrEmpty(el.type))
                        {
                            // elementy zewnętrzne
                            if (externalElementShow)
                            {
                                widthDim = new LinearDimension(wStart, wEnd, (el.y2 - el.y1) / 2, 0.0, dimStyle);
                                addDim = true;
                            }

                            // widok operational
                            if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Operational && view.Name == ViewName.Operational && notForBlock)
                            {
                                widthDim = new LinearDimension(wStart, wEnd, (el.y2 - el.y1) / 2 - profileOffset, 0.0, dimStyle);
                                addDim = true;
                            }

                            // widok back
                            if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable) || el.label == Lab.Frame) && el.label == Lab.Back && view.Name == ViewName.Back && notForBlock)
                            {
                                widthDim = new LinearDimension(wStart, wEnd, (el.y2 - el.y1) / 2, 0.0, dimStyle);
                                addDim = true;
                            }

                            // widok up
                            if (el.type == Lab.Wall && el.label == Lab.Up && view.Name == ViewName.Up && notForBlock)
                            {
                                widthDim = new LinearDimension(wStart, wEnd, (el.z2 - el.z1) / 2, 0.0, dimStyle);
                                addDim = true;
                            }

                            // widok down
                            if ((el.label == Lab.Down_Wall || el.label == Lab.Down_DrainTray) && view.Name == ViewName.Down && notForBlock)
                            {
                                widthDim = new LinearDimension(wStart, wEnd, (el.z2 - el.z1) / 2, 0.0, dimStyle);
                                addDim = true;
                            }
                        }

                        if (el.View == view.Name)
                        {
                            if (el.label == Lab.Function && !(el.View == ViewName.Operational /*|| el.View == ViewName.Back*/)) return;
                            if (el.label == Lab.Function || (el.label == Lab.Block && el.View == ViewName.RightFront) || Lab.ExternalElements.Any(l => l == el.label) || addDim)
                            {
                                widthDim.Layer = layer;
                                dxf.Entities.Add(widthDim);
                            }                               
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
                                addDim = true;
                            }

                            // widok operational
                            if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Operational && view.Name == ViewName.Operational && notForBlock)
                            {
                                heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle);
                                addDim = true;
                            }

                            // widok back
                            if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Back && view.Name == ViewName.Back && notForBlock)
                            {
                                heightDim = new LinearDimension(hStart, hEnd, dimOffset + 100, 90.0, dimStyle);
                                addDim = true;
                            }

                            // widok up
                            if ((el.type == Lab.Wall || el.type == Lab.Door || el.type.Contains(Lab.Removable)) && el.label == Lab.Up && view.Name == ViewName.Up && notForBlock)
                            {
                                heightDim = new LinearDimension(hStart, hEnd, dimOffset + 100, 90.0, dimStyle);
                                addDim = true;
                            }

                            // widok down
                            if ((el.label == Lab.Down || el.label == Lab.Down_DrainTray || el.label == Lab.Down_Wall) && view.Name == Lab.Down && notForBlock)
                            {                          
                                heightDim = new LinearDimension(hStart, hEnd, dimOffset + ((el.x2 - el.x1)/3), 90.0, dimStyle);
                                addDim = true;
                            }
                        }

                        if (el.View == view.Name)
                        {
                            if (el.label == Lab.Function || (el.label == Lab.Block && el.View == ViewName.RightFront) || Lab.ExternalElements.Any(l => l == el.label) || addDim)
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
                case "UpUp":
                    return new List<Vector2> { new Vector2(x1, z1), new Vector2(x2, z1), new Vector2(x2, z2), new Vector2(x1, z2) };

                case "Down": // widok z dołu (XZ, odbicie w Z)
                case "DownUp": 
                    double newZ1Down = globalZMax + globalZMin - z1;
                    double newZ2Down = globalZMax + globalZMin - z2;
                    return new List<Vector2> { new Vector2(x1, newZ2Down), new Vector2(x2, newZ2Down), new Vector2(x2, newZ1Down), new Vector2(x1, newZ1Down) };

                case "RightFront": // widok z lewej (YZ)
                    //if (el.label == ViewName.LeftFront)
                    //{
                    //    return new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y1), new Vector2(x2, y2), new Vector2(x1, y2) };
                    //}
                    return new List<Vector2> { new Vector2(z1, y1), new Vector2(z2, y1), new Vector2(z2, y2), new Vector2(z1, y2) };

                case "LeftFront": // odbicie w Z
                    double newZ1 = globalZMax + globalZMin - z1;
                    double newZ2 = globalZMax + globalZMin - z2;
                    //if (el.label == ViewName.RightFront)
                    //{
                    //    return new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y1), new Vector2(x2, y2), new Vector2(x1, y2) };
                    //}
                    return new List<Vector2> { new Vector2(newZ2, y1), new Vector2(newZ1, y1), new Vector2(newZ1, y2), new Vector2(newZ2, y2) };

                case "Frame": // widok z dołu (XZ, odbicie w Z) - jak dla Down-a
                case "FrameUp":
                    double frameZ1Down = globalZMax + globalZMin - z1;
                    double frameZ2Down = globalZMax + globalZMin - z2;
                    return new List<Vector2> { new Vector2(x1, frameZ2Down), new Vector2(x2, frameZ2Down), new Vector2(x2, frameZ1Down), new Vector2(x1, frameZ1Down) };

                case "Roof": // widok z dołu (XZ, odbicie w Z) - jak dla Down-a
                case "RoofUp":
                    return new List<Vector2> { new Vector2(x1, z1), new Vector2(x2, z1), new Vector2(x2, z2), new Vector2(x1, z2) };

                default:
                    return new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y1), new Vector2(x2, y2), new Vector2(x1, y2) };
            }
        }

        // Wstawia tekst na środku całej jednostki (po X) i "w profilu" (na górnym profilu – w połowie jego grubości)
        private void AddWatermarkText(DxfDocument dxf, Layer textLayer, IEnumerable<Coordinates> allElements, ViewElement view, string textValue, double textHeight = 35)
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

            // wyliczamy środek po X oraz „górę” prostokąta po Y, a następnie schodzimy o połowę grubości profilu
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
    }
}
