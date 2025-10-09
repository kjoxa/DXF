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
        bool frameXYmoved = false;

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
                var numberUp = new Text("2", new Vector3(view.XOffset - 500, view.YOffset + upChannel.y2 - (upChannel.y2 - upChannel.y1) / 2 - 300 / 2, 0), 300)
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
                var numberDown = new Text("1", new Vector3(view.XOffset - 500, view.YOffset + downChannel.y2 - (downChannel.y2 - downChannel.y1) / 2 - 300 / 2, 0), 300)
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
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), 300)
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
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), 300)
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
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), 300)
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
                var numberUp = new Text("2", new Vector3(view.XOffset + x1, view.YOffset + 500, 0), 300)
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

                // numery kanałów                
                switch (view.Name)
                {
                    case ViewName.Operational:
                    case ViewName.Back:
                    case ViewName.Up:
                    case ViewName.UpUp:
                    case ViewName.Down:
                    case ViewName.DownUp:
                    case ViewName.Frame:
                    case ViewName.FrameUp:
                    case ViewName.Roof:
                    case ViewName.RoofUp:
                        GenerateChannelNumbers(elements, dxf, view, textLayer);
                        break;
                }

                // wyodrębnienie elementów dla grupy
                var groupElements = elements
                    .Where(e => elementsGroup
                    .Any(g => string.Equals(g, e.label, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                bool externalElementShow = false;
                int externalElementsYOffset = 0;

                // widoki boczne: przycinanie
                //if (view.Name == "LeftFront" || view.Name == "RightFront")
                //{
                //    GenerateSideView(dxf, elements, elementsGroup, createDimension, createShape, layer, view.Name);
                //}
                //else
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
                            if (el.label == Lab.Block && el.View == view.Name)
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

                            if (!string.IsNullOrEmpty(el.type) && el.label != Lab.Block &&
                                (el.View == view.Name || el.label == Lab.Hole))
                            {
                                if (view.Name is (ViewName.LeftFront or ViewName.RightFront))
                                {
                                    if (Lab.ExternalElements.Any(l => l == el.label) && (el.View == view.Name))
                                    {
                                        var firstblock = view.Name is ViewName.LeftFront ?
                                            elements.OrderBy(e => e.x1).FirstOrDefault(e => e.label == Lab.Block) :
                                            elements.OrderBy(e => e.x2).FirstOrDefault(e => e.label == Lab.Block);
                                        
                                        if (el.label is (Lab.AD or Lab.FC))
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
                                    if (Lab.ExternalElements.Any(l => l == el.label) && (el.View == view.Name) 
                                        || (el.label == Lab.Hole && view.Name is (ViewName.Operational or ViewName.Back)))
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
                                    if (view.Name == ViewName.Operational || view.Name == ViewName.Back)
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

                                //if ((el.View == view.Name && el.type != Lab.Wall)
                                //    || (el.View == view.Name && externalElementShow) // el.zewnetrzne
                                //    || (el.type == Lab.Wall && el.View == view.Name))
                                //{
                                //    dxf.Entities.Add(outerPoly); // &&*
                                //}

                                if (el.label == view.Name ||
                                   (view.Name is (ViewName.Down or ViewName.DownUp) && el.label is (Lab.Down_Div or Lab.Down_DrainTray or Lab.Down_Wall)) ||
                                   (view.Name is (ViewName.Up) && el.label is Lab.Wall) ||
                                   (view.Name is (ViewName.UpUp) && el.label is Lab.Up) ||
                                   (externalElementShow && el.View == view.Name)
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

                                                if (!view.Name.ToLower().Contains("front")                                                    
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

                                if (view.Name != ViewName.Frame && el.label == Lab.Frame && frameXYmoved)
                                {
                                    PrepareFrameToDraw(elements, textLayer, dxf, true);
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

                            if (el.View == view.Name && (el.label == Lab.Function || el.label == Lab.Block || Lab.ExternalElements.Any(l => l == el.label)))
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

                            if (el.View == view.Name && (el.label == Lab.Function || el.label == Lab.Block || Lab.ExternalElements.Any(l => l == el.label)))
                            {
                                heightDim.Layer = layer;
                                dxf.Entities.Add(heightDim);
                            }

                            if (view.Name != ViewName.Frame && el.label == Lab.Frame && frameXYmoved)
                            {
                                dxf.Entities.Remove(heightDim);
                                dxf.Entities.Remove(widthDim);
                            }
                        }
                    }
                }
            }
        }

        void PrepareFrameToDraw(List<Coordinates> elements, Layer textLayer, DxfDocument dxf, bool backToDefault)
        {
            // przywracamy poprzednie rozsunięcie
            if (backToDefault)
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i].label == Lab.Frame && elements[i].y1 > 200)
                    {
                        var tmp = elements[i];
                        tmp.x1 -= 10000;
                        tmp.x2 -= 10000;
                        elements[i] = tmp;
                    }
                }
            }
            else
            {
                //górne ramy muszą dostać przesunięcie o X
                bool upFrameExist = false;
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i].label == Lab.Frame && elements[i].y1 > 200)
                    {
                        var tmp = elements[i];
                        tmp.x1 += 10000;
                        tmp.x2 += 10000;
                        elements[i] = tmp;
                        upFrameExist = true;
                    }
                }

                if (upFrameExist)
                {
                    var firstUpFrame = elements.FirstOrDefault(e => e.label == Lab.Frame && e.y1 > 200);
                    var text = new Text("Frame Up", new Vector3(firstUpFrame.x1 + Views.Frame.XOffset, firstUpFrame.z1 - 440 + Views.Frame.YOffset, 0), 100)
                    {
                        Layer = textLayer,
                        Rotation = 0,
                        Color = AciColor.LightGray,
                        WidthFactor = 1.2,
                    };
                    dxf.Entities.Add(text);
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
            if (viewName == "LeftFront")
            {
                double a = ReflectZ(el.z2);
                double b = ReflectZ(el.z1);
                zLeft = Math.Min(a, b);
                zRight = Math.Max(a, b);
            }
            else // RightFront
            {
                double a = el.z1;
                double b = el.z2;
                zLeft = Math.Min(a, b);
                zRight = Math.Max(a, b);
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

        public void GenerateSideViewOLDBETTER(
    DxfDocument dxf,
    List<Coordinates> elements,
    List<string> elementsGroup,
    bool createDimension,
    bool createShape,
    Layer layer,
    string viewName)
        {
            var groupElements = elements
                .Where(e => elementsGroup.Any(g => string.Equals(e.label, g, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var blocks = elements.Where(e => e.label == "Block").ToList();
            double viewerPlaneX = viewName == "LeftFront" ? blocks.Min(b => b.x1) : blocks.Max(b => b.x2);

            int LabelPriority(Coordinates e) => e.label switch
            {
                "AD" or "FC" or "Function" => 0,
                "Block" => 10,
                _ => 5
            };
            double DepthFromSide(Coordinates e) => viewName == "LeftFront" ? (e.x1 - viewerPlaneX) : (viewerPlaneX - e.x2);
            bool Overlap(double a1, double a2, double b1, double b2) => Math.Max(a1, b1) < Math.Min(a2, b2) - 1e-6;

            var sideElements = groupElements
                .Select(e => new { E = e, D = DepthFromSide(e) })
                .OrderBy(x => x.D)
                .ThenBy(x => LabelPriority(x.E))
                .Select(x => x.E)
                .ToList();

            var frontBlock = blocks
                .Select(b => new { B = b, D = DepthFromSide(b) })
                .OrderBy(x => x.D)
                .FirstOrDefault()
                ?.B;

            var yOffset = Views[viewName].YOffset;
            var xOffset = Views[viewName].XOffset;

            var visibleRects = new List<Rect2D>();

            // ——— Liczenie widoczności (painter: od najbliższych do dalszych) ———
            foreach (var el in sideElements)
            {
                // stabilne filtrowanie AD/FC
                if ((el.label == "AD" || el.label == "FC") && frontBlock != null &&
                    !Overlap(el.x1, el.x2, frontBlock.x1, frontBlock.x2))
                    continue;

                GetProjectedZ(el, viewName, out double zLeft, out double zRight);
                var rect = new Rect2D(el.y1, el.y2, zLeft, zRight, el.label);

                // odejmujemy wszystko, co już "leży bliżej kamery"
                var toAdd = new List<Rect2D> { rect };
                foreach (var occ in visibleRects)
                {
                    if (toAdd.Count == 0) break;
                    var next = new List<Rect2D>();
                    foreach (var r in toAdd) next.AddRange(r.Subtract(occ));
                    toAdd = next;
                }

                if (toAdd.Count > 0) visibleRects.AddRange(toAdd);
            }

            // ——— Rysowanie wyłącznie widocznych fragmentów ———
            foreach (var r in visibleRects)
            {
                var outer2D = r.ToVertices().Select(v => new Vector2(v.X + xOffset, v.Y + yOffset)).ToList();

                if (createShape)
                {
                    var outerPoly = new Polyline2D(outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                    { Layer = layer };
                    dxf.Entities.Add(outerPoly);

                    if (r.SourceLabel == "Block")
                    {
                        double t = 50.0;
                        double iZ1 = r.Z1 + t, iZ2 = r.Z2 - t, iY1 = r.Y1 + t, iY2 = r.Y2 - t;
                        if (iZ2 > iZ1 && iY2 > iY1)
                        {
                            var inner2D = new List<Vector2>
                    {
                        new Vector2(iZ1 + xOffset, iY1 + yOffset),
                        new Vector2(iZ2 + xOffset, iY1 + yOffset),
                        new Vector2(iZ2 + xOffset, iY2 + yOffset),
                        new Vector2(iZ1 + xOffset, iY2 + yOffset),
                    };
                            var innerPoly = new Polyline2D(inner2D.Select(p => new Polyline2DVertex(p.X, p.Y, 0)).ToList(), true)
                            { Layer = layer };
                            dxf.Entities.Add(innerPoly);
                        }

                        // narożniki do środka (jak u Ciebie)
                        for (int i = 0; i < outer2D.Count; i++)
                        {
                            var c = outer2D[i];
                            double dx = 0, dy = 0;
                            switch (i)
                            {
                                case 0: dx = t; dy = t; break;
                                case 1: dx = -t; dy = t; break;
                                case 2: dx = -t; dy = -t; break;
                                case 3: dx = t; dy = -t; break;
                            }
                            var extra = 20.0;
                            int sx = Math.Sign(dx), sy = Math.Sign(dy);

                            var cornerVertices = new List<Polyline2DVertex>
                    {
                        new(c.X,                      c.Y,                      0),
                        new(c.X + dx + extra * sx,    c.Y,                      0),
                        new(c.X + dx + extra * sx,    c.Y + dy,                 0),
                        new(c.X + dx,                 c.Y + dy,                 0),
                        new(c.X + dx,                 c.Y + dy + extra * sy,    0),
                        new(c.X,                      c.Y + dy + extra * sy,    0),
                    };

                            var cornerPoly = new Polyline2D(cornerVertices, true) { Layer = layer };
                            var hatch = new Hatch(HatchPattern.Solid, true) { Layer = layer, Color = new AciColor(7) };
                            hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));
                            dxf.Entities.Add(hatch);
                        }
                    }
                }

                if (createDimension)
                {
                    double dimOffset = 30.0;
                    var wStart = outer2D[0]; var wEnd = outer2D[1];
                    dxf.Entities.Add(new LinearDimension(wStart, wEnd, -dimOffset, 0.0, dimStyle) { Layer = layer });

                    var hStart = outer2D[1]; var hEnd = outer2D[2];
                    dxf.Entities.Add(new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle) { Layer = layer });
                }
            }

            // watermark tylko raz
            AddWatermarkText(dxf, layer, elements, Views[viewName], Views.GetWaterMark());
        }


        public void GenerateSideView(
    DxfDocument dxf,
    List<Coordinates> elements,
    List<string> elementsGroup,
    bool createDimension,
    bool createShape,
    Layer layer,
    string viewName)
        {
            const double EPS = 1e-3;

            // Zwraca część o, która leży POZA maską m (o \ m). Brak niespodzianek, brak „odwrócenia”.
            List<Rect2D> SliceOutside(Rect2D o, Rect2D m)
            {
                // szybkie wyjście: brak nakładania
                if (o.Z2 <= m.Z1 + EPS || o.Z1 >= m.Z2 - EPS ||
                    o.Y2 <= m.Y1 + EPS || o.Y1 >= m.Y2 - EPS)
                    return new List<Rect2D> { o };

                var res = new List<Rect2D>();

                // lewa część
                if (o.Z1 < m.Z1 - EPS)
                    res.Add(new Rect2D(o.Y1, o.Y2, o.Z1, Math.Min(o.Z2, m.Z1), o.SourceLabel));

                // prawa część
                if (o.Z2 > m.Z2 + EPS)
                    res.Add(new Rect2D(o.Y1, o.Y2, Math.Max(o.Z1, m.Z2), o.Z2, o.SourceLabel));

                // dolna i górna (tylko w zakresie wspólnego Z)
                double zLo = Math.Max(o.Z1, m.Z1);
                double zHi = Math.Min(o.Z2, m.Z2);
                if (zHi - zLo > EPS)
                {
                    if (o.Y1 < m.Y1 - EPS)
                        res.Add(new Rect2D(o.Y1, Math.Min(o.Y2, m.Y1), zLo, zHi, o.SourceLabel));
                    if (o.Y2 > m.Y2 + EPS)
                        res.Add(new Rect2D(Math.Max(o.Y1, m.Y2), o.Y2, zLo, zHi, o.SourceLabel));
                }

                return res.Where(r => (r.Z2 - r.Z1) > EPS && (r.Y2 - r.Y1) > EPS).ToList();
            }

            bool Overlap1D_Strict(double a1, double a2, double b1, double b2, double eps)
            {
                // zachodzi tylko gdy zakresy mają wnętrze wspólne; styki (==) NIE liczą się jako kolizja
                return Math.Min(a2, b2) - Math.Max(a1, b1) > eps;
            }

            // --- offsety widoku ---
            var yOffset = Views[viewName].YOffset;
            var xOffset = Views[viewName].XOffset;

            // --- dane wejściowe ---
            var groupElements = elements
                .Where(e => elementsGroup.Any(g => string.Equals(e.label, g, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var blocksAll = elements.Where(e => e.label == "Block").ToList();
            var frontBlock = (viewName == "LeftFront")
    ? blocksAll.OrderBy(b => b.x1).First()
    : blocksAll.OrderByDescending(b => b.x2).First();
            if (blocksAll.Count == 0) return;

            // --- kierunek patrzenia ---
            double viewerPlaneX = viewName == "LeftFront" ? blocksAll.Min(b => b.x1) : blocksAll.Max(b => b.x2);
            double DepthFromSide(Coordinates e) => viewName == "LeftFront" ? (e.x1 - viewerPlaneX) : (viewerPlaneX - e.x2);

            // --- pomocnicze ---
            
            bool Eq(double a, double b) => Math.Abs(a - b) <= EPS;
            bool Overlap1D(double a1, double a2, double b1, double b2) => Math.Max(a1, b1) < Math.Min(a2, b2) - EPS;

            void GetBlockProjectedBounds(Coordinates block, out double bz1, out double bz2, out double by1, out double by2)
            {
                GetProjectedZ(block, viewName, out double pz1, out double pz2);
                bz1 = Math.Min(pz1, pz2);
                bz2 = Math.Max(pz1, pz2);
                by1 = block.y1; by2 = block.y2;
            }

            // ========= FAZA 1 (NAPRAWIONA): widoczność względem UNII bliższych bloków =========
            // ========= FAZA 1 (bez Subtract): widoczność względem UNII bliższych bloków =========
            var blocksByDepth = blocksAll.OrderBy(DepthFromSide).ToList(); // najbliższy -> najdalszy

            // rzut prostokąta bloku na widok
            Rect2D ProjBlock(Coordinates b)
            {
                GetProjectedZ(b, viewName, out double z1, out double z2);
                return new Rect2D(b.y1, b.y2, Math.Min(z1, z2), Math.Max(z1, z2), "Block");
            }

            // „unia” bliższych bloków jako lista nieprzecinających się prostokątów
            var nearerUnion = new List<Rect2D>();

            // wynik: widoczne fragmenty + właściciel
            var visibleBlocks = new List<(Rect2D rect, Coordinates owner)>();

            
            visibleBlocks.Clear();

            foreach (var b in blocksByDepth)
            {
                var rect = ProjBlock(b);

                // 1) widoczne części = rect MINUS (unia bliższych)
                var toDraw = new List<Rect2D> { rect };
                foreach (var mask in nearerUnion)
                {
                    if (toDraw.Count == 0) break;
                    var next = new List<Rect2D>();
                    foreach (var r in toDraw)
                        next.AddRange(SliceOutside(r, mask));   // <<< ZAMIANA: własne odejmowanie
                    toDraw = next;
                }
                visibleBlocks.AddRange(toDraw.Select(r => (r, b)));

                // 2) zaktualizuj UNIĘ bliższych: dodaj PEŁNY rect bieżącego bloku
                var addPieces = new List<Rect2D> { rect };
                foreach (var mask in nearerUnion)
                {
                    if (addPieces.Count == 0) break;
                    var next = new List<Rect2D>();
                    foreach (var r in addPieces)
                        next.AddRange(SliceOutside(r, mask));   // <<< ZAMIANA: własne odejmowanie
                    addPieces = next;
                }
                nearerUnion.AddRange(addPieces);
            }

            // ========= RYSOWANIE bloków =========
            var drawnCornerKeys = new HashSet<string>();

            foreach (var (r, owner) in visibleBlocks)
            {
                var outer2D = r.ToVertices().Select(v => new Vector2(v.X + xOffset, v.Y + yOffset)).ToList();

                if (createShape)
                {
                    // zewnętrzny obrys – zawsze
                    var outerPoly = new Polyline2D(
                        outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true
                    )
                    { Layer = layer };
                    dxf.Entities.Add(outerPoly);

                    // ===== WEWNĘTRZNY PROFIL (dla KAŻDEGO bloku), bazując na pełnym ownerze i CLIP do r =====
                    const double t = 50.0; // profileThickness

                    // 1) bazowe granice "inner" z pełnego bloku (owner), nie z r
                    GetProjectedZ(owner, viewName, out double oz1, out double oz2);
                    double bz1 = Math.Min(oz1, oz2), bz2 = Math.Max(oz1, oz2);
                    double by1 = owner.y1, by2 = owner.y2;

                    double iZ1 = bz1 + t, iZ2 = bz2 - t;
                    double iY1 = by1 + t, iY2 = by2 - t;

                    if (iZ2 - iZ1 > EPS && iY2 - iY1 > EPS)
                    {
                        // pion lewy (Z = iZ1)
                        if (iZ1 > r.Z1 + EPS && iZ1 < r.Z2 - EPS)
                        {
                            double yA = Math.Max(iY1, r.Y1), yB = Math.Min(iY2, r.Y2);
                            if (yB - yA > EPS)
                                dxf.Entities.Add(new Line(
                                    new Vector2(iZ1 + xOffset, yA + yOffset),
                                    new Vector2(iZ1 + xOffset, yB + yOffset))
                                { Layer = layer });
                        }

                        // pion prawy (Z = iZ2)
                        if (iZ2 > r.Z1 + EPS && iZ2 < r.Z2 - EPS)
                        {
                            double yA = Math.Max(iY1, r.Y1), yB = Math.Min(iY2, r.Y2);
                            if (yB - yA > EPS)
                                dxf.Entities.Add(new Line(
                                    new Vector2(iZ2 + xOffset, yA + yOffset),
                                    new Vector2(iZ2 + xOffset, yB + yOffset))
                                { Layer = layer });
                        }

                        // poziom dolny (Y = iY1)
                        if (iY1 > r.Y1 + EPS && iY1 < r.Y2 - EPS)
                        {
                            double zA = Math.Max(iZ1, r.Z1), zB = Math.Min(iZ2, r.Z2);
                            if (zB - zA > EPS)
                                dxf.Entities.Add(new Line(
                                    new Vector2(zA + xOffset, iY1 + yOffset),
                                    new Vector2(zB + xOffset, iY1 + yOffset))
                                { Layer = layer });
                        }

                        // poziom górny (Y = iY2)
                        if (iY2 > r.Y1 + EPS && iY2 < r.Y2 - EPS)
                        {
                            double zA = Math.Max(iZ1, r.Z1), zB = Math.Min(iZ2, r.Z2);
                            if (zB - zA > EPS)
                                dxf.Entities.Add(new Line(
                                    new Vector2(zA + xOffset, iY2 + yOffset),
                                    new Vector2(zB + xOffset, iY2 + yOffset))
                                { Layer = layer });
                        }
                    }
                    // ===== koniec: wewnętrzny profil =====

                    // narożniki tylko na rogach zewnętrznych ownera
                    GetBlockProjectedBounds(owner, out double bzL, out double bzR, out double bY1, out double bY2);

                    bool IsOuterCorner(int idx) => idx switch
                    {
                        0 => Eq(r.Z1, bzL) && Eq(r.Y1, bY1),
                        1 => Eq(r.Z2, bzR) && Eq(r.Y1, bY1),
                        2 => Eq(r.Z2, bzR) && Eq(r.Y2, bY2),
                        3 => Eq(r.Z1, bzL) && Eq(r.Y2, bY2),
                        _ => false
                    };

                    const double cornerT = 50.0, extra = 20.0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (!IsOuterCorner(i)) continue;

                        string cornerTag = i switch { 0 => "LL", 1 => "LR", 2 => "UR", 3 => "UL", _ => "X" };
                        string key = $"{owner.GetHashCode()}_{cornerTag}";
                        if (!drawnCornerKeys.Add(key)) continue;

                        var c = outer2D[i];
                        double dx = 0, dy = 0;
                        switch (i)
                        {
                            case 0: dx = cornerT; dy = cornerT; break;
                            case 1: dx = -cornerT; dy = cornerT; break;
                            case 2: dx = -cornerT; dy = -cornerT; break;
                            case 3: dx = cornerT; dy = -cornerT; break;
                        }
                        int sx = Math.Sign(dx), sy = Math.Sign(dy);

                        var cornerVertices = new List<Polyline2DVertex>
            {
                new(c.X,                      c.Y,                      0),
                new(c.X + dx + extra * sx,    c.Y,                      0),
                new(c.X + dx + extra * sx,    c.Y + dy,                 0),
                new(c.X + dx,                 c.Y + dy,                 0),
                new(c.X + dx,                 c.Y + dy + extra * sy,    0),
                new(c.X,                      c.Y + dy + extra * sy,    0),
            };

                        var cornerPoly = new Polyline2D(cornerVertices, true) { Layer = layer };
                        var hatch = new Hatch(HatchPattern.Solid, true) { Layer = layer, Color = new AciColor(7) };
                        hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));
                        dxf.Entities.Add(hatch);
                    }
                    }

                    if (createDimension)
                {
                    double dimOffset = 30.0;
                    var wStart = outer2D[0]; var wEnd = outer2D[1];
                    dxf.Entities.Add(new LinearDimension(wStart, wEnd, -dimOffset, 0.0, dimStyle) { Layer = layer });
                    var hStart = outer2D[1]; var hEnd = outer2D[2];
                    dxf.Entities.Add(new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle) { Layer = layer });
                }
            }

            // ========= FAZA 2: overlaye (Function/AD/FC...) – prosto: przyklej i nie rysuj krawędzi na licu =========
            // ========= FAZA 2: overlaye (Function/AD/FC...) — bez cięcia, tylko tłumienie krawędzi na licu bloku =========
            var overlays = groupElements
                .Where(e => e.label != "Block")
                .OrderBy(DepthFromSide)
                .ToList();

            // Zbierz krawędzie widocznych fragmentów bloków (po FAZIE 1)
            var blockEdgesH = visibleBlocks.SelectMany(vb => new[]
            {
    new { Y = vb.rect.Y1, Z1 = vb.rect.Z1, Z2 = vb.rect.Z2 },
    new { Y = vb.rect.Y2, Z1 = vb.rect.Z1, Z2 = vb.rect.Z2 },
}).ToList();

            var blockEdgesV = visibleBlocks.SelectMany(vb => new[]
            {
    new { Z = vb.rect.Z1, Y1 = vb.rect.Y1, Y2 = vb.rect.Y2 },
    new { Z = vb.rect.Z2, Y1 = vb.rect.Y1, Y2 = vb.rect.Y2 },
}).ToList();

            bool OnBlockHorizontalEdge(double yConst, double z1, double z2)
                => blockEdgesH.Any(e => Math.Abs(yConst - e.Y) <= EPS && Overlap1D(z1, z2, e.Z1, e.Z2));

            bool OnBlockVerticalEdge(double zConst, double y1, double y2)
                => blockEdgesV.Any(e => Math.Abs(zConst - e.Z) <= EPS && Overlap1D(y1, y2, e.Y1, e.Y2));

            foreach (var el in overlays)
            {
                GetProjectedZ(el, viewName, out double zL, out double zR);
                double Z1 = Math.Min(zL, zR), Z2 = Math.Max(zL, zR);
                double Y1 = el.y1, Y2 = el.y2;

                // punkty narożne overlayu w rzutni
                var p00 = new Vector2(Z1 + xOffset, Y1 + yOffset);
                var p10 = new Vector2(Z2 + xOffset, Y1 + yOffset);
                var p11 = new Vector2(Z2 + xOffset, Y2 + yOffset);
                var p01 = new Vector2(Z1 + xOffset, Y2 + yOffset);

                // rysuj TYLKO te boki, które nie kładą się na krawędzi widocznego bloku
                // dół (Y = Y1)
                if (!OnBlockHorizontalEdge(Y1, Z1, Z2))
                    dxf.Entities.Add(new Line(p00, p10) { Layer = layer });

                // góra (Y = Y2)
                if (!OnBlockHorizontalEdge(Y2, Z1, Z2))
                    dxf.Entities.Add(new Line(p11, p01) { Layer = layer });

                // lewa (Z = Z1)
                if (!OnBlockVerticalEdge(Z1, Y1, Y2))
                    dxf.Entities.Add(new Line(p01, p00) { Layer = layer });

                // prawa (Z = Z2)
                if (!OnBlockVerticalEdge(Z2, Y1, Y2))
                    dxf.Entities.Add(new Line(p10, p11) { Layer = layer });

                if (createDimension)
                {
                    double dimOffset = 30.0;
                    dxf.Entities.Add(new LinearDimension(p00, p10, -dimOffset, 0.0, dimStyle) { Layer = layer });
                    dxf.Entities.Add(new LinearDimension(p10, p11, dimOffset, 90.0, dimStyle) { Layer = layer });
                }
            }

            // watermark raz
            AddWatermarkText(dxf, layer, elements, Views[viewName], Views.GetWaterMark());
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

                case "LeftFront": // widok z lewej (YZ)
                    return new List<Vector2> { new Vector2(z1, y1), new Vector2(z2, y1), new Vector2(z2, y2), new Vector2(z1, y2) };

                case "RightFront": // odbicie w Z
                    double newZ1 = globalZMax + globalZMin - z1;
                    double newZ2 = globalZMax + globalZMin - z2;
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
