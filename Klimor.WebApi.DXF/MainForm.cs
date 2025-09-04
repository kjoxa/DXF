using Klimor.WebApi.DXF.Development;
using Klimor.WebApi.DXF.Structures;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Tables;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml.Linq;
using static netDxf.Entities.HatchBoundaryPath;

namespace Klimor.WebApi.DXF
{
    /*
    Rysunki oddalone od siebie w dół i po bokach, ja zostawiam w dół

    WARSTWY
    1. Bloki z narożnikami i wymiarami
    2. Funkcje z ikonami i wymiarami
    3. FL, FR, Op, Back, Up, Down z wymiarami

    */
    public partial class MainFrm : Form
    {
        public MainFrm()
        {
            InitializeComponent();
        }

        private static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>
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

        private void btnOpenJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                ofd.Title = "Wybierz plik JSON";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(ofd.FileName);
                        List<Element> elements = JsonSerializer.Deserialize<List<Element>>(json);

                        elements = elements.Where(e => !string.IsNullOrWhiteSpace(e.label)).ToList();
                        // Tutaj możesz wybrać, czy generujesz 2D czy 3D
                        Generate2D(elements, "output2DNEW.dxf");
                        //GenerateViews(elements, "output2D.dxf");
                        //Generate3D(elements, "output3D.dxf");

                        MessageBox.Show("Pliki DXF zostały wygenerowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);                        
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Błąd podczas wczytywania: " + ex.Message);
                    }
                }
            }
            Application.Exit(); // Zamyka aplikację po zakończeniu
        }

        private void Generate2D(List<Element> elements, string fileOutput)
        {
            var icons = DxfDocument.Load("BLOCKS.dxf");
            var dxf = new DxfDocument();
            var cornerLayer = new Layer("CornerFill") { Color = new AciColor(7) };
            var textLayer = new Layer("Text_Views") { Color = AciColor.Blue };

            var dimStyle = new DimensionStyle("MyDimStyle")
            {
                TextHeight = 15.0,
                ArrowSize = 25.5,
                LengthPrecision = 0,
                DimLineColor = AciColor.Yellow,
                ExtLineColor = AciColor.Yellow,
                TextColor = AciColor.Yellow
            };

            dxf.DimensionStyles.Add(dimStyle);
            var views = new List<(string name, double yOffset)>
            {
                ("Operational", 0),
                ("Back", 2000),
                ("Up", 4000),
                ("Down", 6000),
                ("LeftFront", 8000),
                ("RightFront", 10000)
            };
            
            double profileOffset = 50.0;
            double cornerSize = 50.0;

            // wyznaczenie globalnych min/max dla wszystkich elementów ---
            double globalXMin = elements.Min(e => e.x1);
            double globalXMax = elements.Max(e => e.x2);
            double globalYMin = elements.Min(e => e.y1);
            double globalYMax = elements.Max(e => e.y2);
            double globalZMin = elements.Min(e => e.z1);
            double globalZMax = elements.Max(e => e.z2);

            void GenerateBlocks()
            {
                var layer = dxf.Layers.Add(new Layer("Block") { Color = AciColor.Default });
                GenerateView(new List<string> { "Block" }, false, true, layer);
            }

            void GenerateBlockDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Block_dimensions") { Color = AciColor.DarkGray });
                GenerateView(new List<string> { "Block" }, true, false, layer);
            }

            void GenerateFunctionsWithIcons()
            {
                var upOffset = views.FirstOrDefault(v => v.name == "Up").yOffset;
                var iconsList = icons.Blocks.ToList();
                var layer = dxf.Layers.Add(new Layer("Function") { Color = new AciColor(4) });
                GenerateView(new List<string> { "Function" }, false, true, layer);                
                
                // dodawanie ikon
                var sName = string.Empty;                
                var distinctList = elements.DistinctBy(e => (e.posUpDown, e.x1, e.y1)).Where(i => i.label.Contains("icon") && i.additionalInfos != null).ToList();
                foreach (var icon in distinctList)
                {                    
                    sName = icon.additionalInfos.iconName;
                    bool isExhaust = icon.additionalInfos.airPath.ToLower() == "exhaust";
                    if (IconMap.ContainsKey(sName!))
                    {
                        var insertIcon = iconsList.FirstOrDefault(b => b.Name.Equals(sName, StringComparison.OrdinalIgnoreCase));
                        switch (icon.additionalInfos.iconPosition)
                        {
                            case "Operational":
                                var insertIconOperational = new Insert(insertIcon)
                                {
                                    Position = new Vector3(icon.x1, icon.y1, 0), // przesunięcie w bok
                                    Layer = layer,
                                    Scale = new Vector3(1, 1, 1)
                                };
                                if (!isExhaust && icon.additionalInfos.sName == "VF")
                                {
                                    insertIconOperational.Position = new Vector3(icon.x1 + (icon.x2 - icon.x1), icon.y1, 0);
                                    insertIconOperational.Scale = new Vector3(-1, 1, 1);
                                }                                    

                                if (icon.additionalInfos.iconPosition != "Back")
                                dxf.Entities.Add(insertIconOperational);
                                break;

                            case "Up":
                                var insertIconUp = new Insert(insertIcon)
                                {
                                    Position = new Vector3(icon.x1, icon.z1 + upOffset, 0),
                                    Layer = layer,                                    
                                };
                                if (!isExhaust && icon.additionalInfos.sName == "VF")
                                {
                                    insertIconUp.Position = new Vector3(icon.x1 + (icon.x2 - icon.x1), icon.z1 + upOffset, 0);
                                    insertIconUp.Scale = new Vector3(-1, 1, 1);
                                }                                    

                                dxf.Entities.Add(insertIconUp);
                                break;

                            default:
                                break;
                        }
                    }
                }
            }

            void GenerateFunctionsDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Function_dimensions") { Color = AciColor.Green });
                GenerateView(new List<string> { "Function" }, true, false, layer);
            }

            void GenerateExternalElements()
            {
                var layer = dxf.Layers.Add(new Layer("ExternalElements") { Color = AciColor.Magenta });
                GenerateView(new List<string> { "AD", "FC", "INTK" }, false, true, layer);

                var layerDim = dxf.Layers.Add(new Layer("ExternalElements_dimensions") { Color = AciColor.Cyan });
                GenerateView(new List<string> { "AD", "FC", "INTK" }, true, false, layerDim);
            }

            void GenerateView_old(List<string> elementsGroup, bool createDimension, bool createShape, Layer layer)
            {
                foreach (var view in views)
                {
                    // podpis widoku przy elemencie
                    var firstElement = elements.FirstOrDefault();
                    if (firstElement != null)
                    {
                        double elementCenterY = (firstElement.y1 + firstElement.y2) / 2 + view.yOffset;
                        var text = new Text(view.name, new Vector3(-1200, elementCenterY + 900, 0), 100) // -300 przesunięcie w lewo
                        {
                            Layer = textLayer,
                            Rotation = 0,
                            Color = AciColor.LightGray,
                            WidthFactor = 1.2,
                        };
                        dxf.Entities.Add(text);
                    }

                    foreach (var el in elements.Where(e => elementsGroup.Any(g => string.Equals(g, e.label, StringComparison.OrdinalIgnoreCase))))
                    {
                        // generowanie współrzędnych dla widoku
                        List<Vector2> outer2D = GenerateViewVertices(el, view.name, globalXMin, globalXMax,
                            globalYMin, globalYMax, globalZMin, globalZMax);
                        List<Vector2> inner2D = outer2D.Select(v => new Vector2(v.X + profileOffset, v.Y - profileOffset)).ToList();

                        // przesunięcie Y dla widoku
                        outer2D = outer2D.Select(v => new Vector2(v.X, v.Y + view.yOffset)).ToList();
                        inner2D = inner2D.Select(v => new Vector2(v.X, v.Y + view.yOffset)).ToList();

                        if (createShape)
                        {
                            // rysowanie zewnętrznej i wewnętrznej polilinii
                            var outerPoly = new Polyline2D(outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                            {
                                Layer = layer
                            };
                            dxf.Entities.Add(outerPoly);
                            

                            // wypełnione narożniki
                            if (el.label == "Block")
                            {
                                // zamiast inner2DArray[0..3] -> rozpoznawanie rogów po min/max
                                var left = inner2D.Min(p => p.X);
                                var right = inner2D.Max(p => p.X);
                                var bottom = inner2D.Min(p => p.Y);
                                var top = inner2D.Max(p => p.Y);

                                var bottomLeft = inner2D.First(p => p.X == left && p.Y == bottom);
                                var bottomRight = inner2D.First(p => p.X == right && p.Y == bottom);
                                var topRight = inner2D.First(p => p.X == right && p.Y == top);
                                var topLeft = inner2D.First(p => p.X == left && p.Y == top);

                                // korekta narożników
                                bottomLeft = new Vector2(bottomLeft.X, bottomLeft.Y + 2*profileOffset);
                                bottomRight = new Vector2(bottomRight.X - 2*profileOffset, bottomRight.Y + 2*profileOffset);
                                topRight = new Vector2(topRight.X - 2*profileOffset, topRight.Y);
                                topLeft = new Vector2(topLeft.X, topLeft.Y);

                                inner2D = new List<Vector2> { bottomLeft, bottomRight, topRight, topLeft };

                                var innerPoly = new Polyline2D(inner2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                                {
                                    Layer = layer
                                };
                                dxf.Entities.Add(innerPoly);

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
                                            
                                            if (!view.name.ToLower().Contains("front"))
                                            {
                                                var text = new Text(el.additionalInfos.blockNumber.ToString(),
                                                new Vector3(c.X + 2*profileOffset, c.Y + 2*profileOffset, 0), 30);

                                                text.Style = new TextStyle("ArialBold", "arialbd.ttf");
                                                text.Layer = layer;
                                                text.Color = new AciColor(7);
                                                dxf.Entities.Add(text);
                                            }
                                            break;

                                        case 1: // prawy dół
                                            cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X, c.Y + profileOffset, 0));
                                            cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y + profileOffset, 0));
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

                                    var cornerPoly = new Polyline2D(cornerVertices, true);
                                    cornerPoly.Layer = layer;

                                    var hatch = new Hatch(HatchPattern.Solid, true);
                                    hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));
                                    hatch.Layer = layer;
                                    hatch.Color = new AciColor(7);

                                    dxf.Entities.Add(hatch);
                                    idx++;
                                }
                                idx = 0;
                            }
                        }

                        if (createDimension)
                        {
                            // wymiary
                            double dimOffset = 30.0;

                            // szerokość (poziomy)
                            var wStart = outer2D[0];
                            var wEnd = outer2D[1];
                            var widthDim = new LinearDimension(wStart, wEnd, -dimOffset, 0.0, dimStyle)
                            {
                                Layer = layer
                            };
                            dxf.Entities.Add(widthDim);

                            // wysokość (pionowy)
                            var hStart = outer2D[1];
                            var hEnd = outer2D[2];
                            var heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle)
                            {
                                Layer = layer
                            };
                            dxf.Entities.Add(heightDim);
                        }
                    }
                }
            }

            void GenerateSideView_old(List<string> elementsGroup, bool createDimension, bool createShape, Layer layer, string viewName)
            {
                var groupElements = elements
                    .Where(e => elementsGroup.Any(g => string.Equals(e.label, g, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var sideElements = groupElements
                    .OrderBy(e => viewName == "LeftFront" ? e.z1 : e.z2)
                    .ToList();

                List<Rect2D> visibleRects = new List<Rect2D>();

                var blocks = elements.Where(e => e.label == "Block").OrderBy(e => e.x1).ToList();
                double secondBlockX1 = blocks.Skip(1).FirstOrDefault()?.x1 ?? double.MaxValue;
                double secondLastBlockX2 = blocks.OrderByDescending(e => e.x2).Skip(1).FirstOrDefault()?.x2 ?? double.MinValue;

                foreach (var el in sideElements)
                {
                    // Filtrowanie AD/FC
                    if ((el.label == "AD" || el.label == "FC") && viewName == "LeftFront" && el.x1 >= secondBlockX1)
                        continue;
                    if ((el.label == "AD" || el.label == "FC") && viewName == "RightFront" && el.x2 <= secondLastBlockX2)
                        continue;

                    var rect = new Rect2D(
                        el.y1,
                        el.y2,
                        Math.Min(viewName == "LeftFront" ? el.z1 : el.z2, viewName == "LeftFront" ? el.z2 : el.z1),
                        Math.Max(viewName == "LeftFront" ? el.z1 : el.z2, viewName == "LeftFront" ? el.z2 : el.z1),
                        el.label
                    );

                    var yOffset = views.FirstOrDefault(v => v.name == viewName).yOffset;
                    if (el.label == "Block")
                    {
                        var outer2DFull = new List<Vector2>
    {
        new Vector2(rect.Z1, rect.Y1 + yOffset),
        new Vector2(rect.Z2, rect.Y1 + yOffset),
        new Vector2(rect.Z2, rect.Y2 + yOffset),
        new Vector2(rect.Z1, rect.Y2 + yOffset)
    };

                        var profile = new Polyline2D(
                            outer2DFull.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(),
                            true
                        )
                        {
                            Layer = layer,
                            Color = new AciColor(7) // np. biały kontur
                        };
                        dxf.Entities.Add(profile);
                    }

                    List<Rect2D> toAdd = new List<Rect2D> { rect };
                    foreach (var existing in visibleRects)
                    {
                        List<Rect2D> newToAdd = new List<Rect2D>();
                        foreach (var r in toAdd)
                            newToAdd.AddRange(r.Subtract(existing));
                        toAdd = newToAdd;
                        if (toAdd.Count == 0) break;
                    }

                    visibleRects.AddRange(toAdd);
                }
                

                // Rysowanie widocznych prostokątów
                foreach (var r in visibleRects)
                {
                    var yOffset = views.FirstOrDefault(v => v.name == viewName).yOffset;
                    List<Vector2> outer2D = r.ToVertices().Select(v => new Vector2(v.X, v.Y + yOffset)).ToList();

                    if (createShape)
                    {
                        var poly = new Polyline2D(
                            outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(),
                            true
                        )
                        {
                            Layer = layer
                        };
                        dxf.Entities.Add(poly);

                        // Narożniki tylko dla bloków
                        if (r.SourceLabel == "Block")
                        {
                            double offset = 50;

                            // Zakładam, że outer2D ma wierzchołki w kolejności: LL, LR, UR, UL
                            for (int i = 0; i < outer2D.Count; i++)
                            {
                                var c = outer2D[i];
                                double dx = 0, dy = 0;

                                switch (i)
                                {
                                    case 0: // lewy-dolny
                                        dx = -offset; dy = -offset;
                                        break;
                                    case 1: // prawy-dolny
                                        dx = offset; dy = -offset;
                                        break;
                                    case 2: // prawy-górny
                                        dx = offset; dy = offset;
                                        break;
                                    case 3: // lewy-górny
                                        dx = -offset; dy = offset;
                                        break;
                                }

                                var cornerVertices = new List<Polyline2DVertex>
    {
        new Polyline2DVertex(c.X, c.Y, 0),
        new Polyline2DVertex(c.X + dx, c.Y, 0),
        new Polyline2DVertex(c.X + dx, c.Y + dy, 0),
        new Polyline2DVertex(c.X, c.Y + dy, 0)
    };

                                var cornerPoly = new Polyline2D(cornerVertices, true);
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

            void GenerateSideView(List<string> elementsGroup, bool createDimension, bool createShape, Layer layer, string viewName)
            {
                var groupElements = elements
                    .Where(e => elementsGroup.Any(g => string.Equals(e.label, g, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // sort: od najbliższego do widoku (LeftFront -> z1, RightFront -> z2)
                var sideElements = groupElements
                    .OrderBy(e => viewName == "LeftFront" ? e.z1 : e.z2)
                    .ToList();

                List<Rect2D> visibleRects = new List<Rect2D>();

                // do filtrów AD/FC
                var blocksAll = elements.Where(e => e.label == "Block").OrderBy(e => e.x1).ToList();
                double secondBlockX1 = blocksAll.Skip(1).FirstOrDefault()?.x1 ?? double.MaxValue;
                double secondLastBlockX2 = blocksAll.OrderByDescending(e => e.x2).Skip(1).FirstOrDefault()?.x2 ?? double.MinValue;

                var yOffset = views.FirstOrDefault(v => v.name == viewName).yOffset;

                // --- 1) Pełne kontury bloków (profil) – rysujemy tylko w przebiegu dla "Block" (createShape) ---
                if (createShape && elementsGroup.Any(g => string.Equals(g, "Block", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var block in sideElements.Where(e => e.label == "Block"))
                    {
                        var rectFull = new Rect2D(
                            block.y1,
                            block.y2,
                            Math.Min(viewName == "LeftFront" ? block.z1 : block.z2, viewName == "LeftFront" ? block.z2 : block.z1),
                            Math.Max(viewName == "LeftFront" ? block.z1 : block.z2, viewName == "LeftFront" ? block.z2 : block.z1),
                            "Block"
                        );

                        var outer2DFull = new List<Vector2>
                        {
                            new Vector2(rectFull.Z1, rectFull.Y1 + yOffset),
                            new Vector2(rectFull.Z2, rectFull.Y1 + yOffset),
                            new Vector2(rectFull.Z2, rectFull.Y2 + yOffset),
                            new Vector2(rectFull.Z1, rectFull.Y2 + yOffset)
                        };

                        var profile = new Polyline2D(
                            outer2DFull.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(),
                            true
                        )
                        {
                            Layer = layer
                        };
                        dxf.Entities.Add(profile);
                    }
                }

                // --- 2) Wylicz widoczne fragmenty (Subtract) + rysowanie ---
                foreach (var el in sideElements)
                {
                    // filtry AD/FC wg drugiego bloku
                    if ((el.label == "AD" || el.label == "FC") && viewName == "LeftFront" && el.x1 >= secondBlockX1) continue;
                    if ((el.label == "AD" || el.label == "FC") && viewName == "RightFront" && el.x2 <= secondLastBlockX2) continue;

                    var rect = new Rect2D(
                        el.y1,
                        el.y2,
                        Math.Min(viewName == "LeftFront" ? el.z1 : el.z2, viewName == "LeftFront" ? el.z2 : el.z1),
                        Math.Max(viewName == "LeftFront" ? el.z1 : el.z2, viewName == "LeftFront" ? el.z2 : el.z1),
                        el.label // ważne: zachowujemy źródło
                    );

                    // odetnij części zasłonięte już widocznymi fragmentami
                    List<Rect2D> toAdd = new List<Rect2D> { rect };
                    foreach (var existing in visibleRects)
                    {
                        var next = new List<Rect2D>();
                        foreach (var r in toAdd)
                            next.AddRange(r.Subtract(existing));
                        toAdd = next;
                        if (toAdd.Count == 0) break;
                    }

                    // dodaj nowe widoczne fragmenty
                    visibleRects.AddRange(toAdd);
                }

                // --- 3) Rysowanie widocznych fragmentów (kształty + wymiary + narożniki tylko dla Block) ---
                foreach (var r in visibleRects)
                {                                        
                    var outer2D = r.ToVertices().Select(v => new Vector2(v.X, v.Y + yOffset)).ToList();

                    if (createShape)
                    {
                        // Zewnętrzny widoczny fragment (po clipie)
                        var outerPoly = new Polyline2D(
                            outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(),
                            true
                        )
                        {
                            Layer = layer
                        };
                        dxf.Entities.Add(outerPoly);

                        // --- Tylko dla BLOCK: inner linia + narożniki do środka ---
                        if (r.SourceLabel == "Block")
                        {
                            double profileThickness = 50.0;

                            // 1) INNER LINIA (wcięcie do środka o profileThickness)
                            // Liczymy z r (bez offsetu), a potem dodajemy yOffset.
                            double iZ1 = r.Z1 + profileThickness;
                            double iZ2 = r.Z2 - profileThickness;
                            double iY1 = r.Y1 + profileThickness;
                            double iY2 = r.Y2 - profileThickness;

                            // rzut do 2D z offsetem
                            if (iZ2 > iZ1 && iY2 > iY1)
                            {
                                var inner2D = new List<Vector2>
                                {
                                    new Vector2(iZ1, iY1 + yOffset), // LL
                                    new Vector2(iZ2, iY1 + yOffset), // LR
                                    new Vector2(iZ2, iY2 + yOffset), // UR
                                    new Vector2(iZ1, iY2 + yOffset)  // UL
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

                            // 2) NAROŻNIKI DO ŚRODKA
                            // outer2D: 0=LL, 1=LR, 2=UR, 3=UL
                            for (int i = 0; i < outer2D.Count; i++)
                            {
                                var c = outer2D[i];

                                // wektor do środka dla każdego rogu:
                                // LL -> (+,+), LR -> (-,+), UR -> (-,-), UL -> (+,-)
                                double dx = 0, dy = 0;
                                switch (i)
                                {
                                    case 0: dx = profileThickness; dy = profileThickness; break; // LL
                                    case 1: dx = -profileThickness; dy = profileThickness; break; // LR
                                    case 2: dx = -profileThickness; dy = -profileThickness; break; // UR
                                    case 3: dx = profileThickness; dy = -profileThickness; break; // UL
                                }

                                // kwadrat narożny skierowany DO ŚRODKA
                                var cornerVertices = new List<Polyline2DVertex>
                                {
                                    new Polyline2DVertex(c.X,         c.Y,         0),
                                    new Polyline2DVertex(c.X + dx,    c.Y,         0),
                                    new Polyline2DVertex(c.X + dx,    c.Y + dy,    0),
                                    new Polyline2DVertex(c.X,         c.Y + dy,    0)
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

            void GenerateView(List<string> elementsGroup, bool createDimension, bool createShape, Layer layer)
            {
                foreach (var view in views)
                {
                    // podpis widoku przy elemencie
                    var firstElement = elements.FirstOrDefault();
                    if (firstElement != null)
                    {
                        double elementCenterY = (firstElement.y1 + firstElement.y2) / 2 + view.yOffset;
                        var text = new Text(view.name, new Vector3(-1200, elementCenterY + 900, 0), 100)
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
                        .Where(e => elementsGroup.Any(g => string.Equals(g, e.label, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    // widoki boczne: przycinanie prostokątów
                    if (view.name == "LeftFront" || view.name == "RightFront")
                    {
                        GenerateSideView(elementsGroup, createDimension, createShape, layer, view.name);
                    }
                    else
                    {
                        // dla innych widoków bez przycinania
                        foreach (var el in groupElements)
                        {
                            // generowanie współrzędnych dla widoku
                            List<Vector2> outer2D = GenerateViewVertices(el, view.name, globalXMin, globalXMax,
                                globalYMin, globalYMax, globalZMin, globalZMax);
                            List<Vector2> inner2D = outer2D.Select(v => new Vector2(v.X + profileOffset, v.Y - profileOffset)).ToList();

                            // przesunięcie Y dla widoku
                            outer2D = outer2D.Select(v => new Vector2(v.X, v.Y + view.yOffset)).ToList();
                            inner2D = inner2D.Select(v => new Vector2(v.X, v.Y + view.yOffset)).ToList();

                            // rysowanie zewnętrznej i wewnętrznej polilinii
                            var outerPoly = new Polyline2D(outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                            {
                                Layer = layer
                            };
                            dxf.Entities.Add(outerPoly);

                            if (createShape)
                            {
                                if (el.label == "Block")
                                {
                                    // zamiast inner2DArray[0..3] -> rozpoznawanie rogów po min/max
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

                                                if (!view.name.ToLower().Contains("front"))
                                                {
                                                    var text = new Text(el.additionalInfos.blockNumber.ToString(),
                                                    new Vector3(c.X + 2 * profileOffset, c.Y + 2 * profileOffset, 0), 30);

                                                    text.Style = new TextStyle("ArialBold", "arialbd.ttf");
                                                    text.Layer = layer;
                                                    text.Color = new AciColor(7);
                                                    dxf.Entities.Add(text);
                                                }
                                                break;

                                            case 1: // prawy dół
                                                cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X, c.Y + profileOffset, 0));
                                                cornerVertices.Add(new Polyline2DVertex(c.X - profileOffset, c.Y + profileOffset, 0));
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

                                        var cornerPoly = new Polyline2D(cornerVertices, true);
                                        cornerPoly.Layer = layer;

                                        var hatch = new Hatch(HatchPattern.Solid, true);
                                        hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));
                                        hatch.Layer = layer;
                                        hatch.Color = new AciColor(7);

                                        dxf.Entities.Add(hatch);
                                        idx++;
                                    }
                                    idx = 0;
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
                                dxf.Entities.Add(widthDim);

                                var hStart = outer2D[1];
                                var hEnd = outer2D[2];
                                var heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle)
                                {
                                    Layer = layer
                                };
                                dxf.Entities.Add(heightDim);
                            }
                        }
                    }
                }
            }

            GenerateBlocks();
            GenerateBlockDimensions();
            GenerateFunctionsWithIcons();
            GenerateFunctionsDimensions();
            GenerateExternalElements();
            dxf.Save(fileOutput);
        }

        private void GenerateViews(List<Element> elements, string filePath)
        {
            bool createDimension = true;
            bool createShape = true;
            var dxf = new DxfDocument();

            // --- Warstwy ---
            var layerNames = elements.Select(e => e.label).Distinct().ToList();
            byte colorIndex = 1;
            foreach (var label in layerNames)
            {
                if (colorIndex > 255) colorIndex = 1;
                dxf.Layers.Add(new Layer(label) { Color = new AciColor(colorIndex) });
                colorIndex++;
            }

            var cornerLayer = new Layer("CornerFill") { Color = new AciColor(7) };
            dxf.Layers.Add(cornerLayer);

            var dimLayer = new Layer("Dimensions") { Color = AciColor.Yellow };
            dxf.Layers.Add(dimLayer);

            var textLayer = new Layer("Text") { Color = AciColor.Blue };
            dxf.Layers.Add(textLayer);

            var dimStyle = new DimensionStyle("MyDimStyle")
            {
                TextHeight = 5.0,
                ArrowSize = 2.5,
                LengthPrecision = 0,
                DimLineColor = AciColor.Yellow,
                ExtLineColor = AciColor.Yellow,
                TextColor = AciColor.Yellow
            };
            dxf.DimensionStyles.Add(dimStyle);

            // --- Definicja widoków ---
            var views = new List<(string name, double yOffset)>
            {
                ("Operational", 0),
                ("Back", 2000),
                ("Up", 4000),
                ("Down", 6000),
                ("LeftFront", 8000),
                ("RightFront", 10000)
            };

            double offset = 5.0; // grubość profilu
            double cornerSize = 5.0;

            // --- Wyznaczenie globalnych min/max dla wszystkich elementów ---
            double globalXMin = elements.Min(e => e.x1);
            double globalXMax = elements.Max(e => e.x2);
            double globalYMin = elements.Min(e => e.y1);
            double globalYMax = elements.Max(e => e.y2);
            double globalZMin = elements.Min(e => e.z1);
            double globalZMax = elements.Max(e => e.z2);

            foreach (var view in views)
            {
                // Podpis widoku przy elemencie
                var firstElement = elements.FirstOrDefault();
                if (firstElement != null)
                {
                    double elementCenterY = (firstElement.y1 + firstElement.y2) / 2 + view.yOffset;
                    var text = new Text(view.name, new Vector3(-1200, elementCenterY + 900, 0), 100) // -300 przesunięcie w lewo
                    {
                        Layer = textLayer,
                        Rotation = 0,
                        Color = AciColor.LightGray,
                        WidthFactor = 1.2,
                    };
                    dxf.Entities.Add(text);
                }

                foreach (var el in elements)
                {
                    // --- Generowanie współrzędnych dla widoku ---
                    List<Vector2> outer2D = GenerateViewVertices(el, view.name, globalXMin, globalXMax,
                        globalYMin, globalYMax, globalZMin, globalZMax);
                    List<Vector2> inner2D = outer2D.Select(v => new Vector2(v.X + offset, v.Y + offset)).ToList();

                    // przesunięcie Y dla widoku
                    outer2D = outer2D.Select(v => new Vector2(v.X, v.Y + view.yOffset)).ToList();
                    inner2D = inner2D.Select(v => new Vector2(v.X, v.Y + view.yOffset)).ToList();

                    if (createShape)
                    {
                        // --- Rysowanie zewnętrznej i wewnętrznej polilinii ---

                        var outerPoly = new Polyline2D(outer2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                        {
                            Layer = dxf.Layers[el.label]
                        };
                        dxf.Entities.Add(outerPoly);

                        var innerPoly = new Polyline2D(inner2D.Select(v => new Polyline2DVertex(v.X, v.Y, 0)).ToList(), true)
                        {
                            Layer = dxf.Layers[el.label]
                        };
                        if (el.label == "Block")
                        {
                            dxf.Entities.Add(innerPoly);
                        }

                        // --- Wypełnione narożniki ---
                        foreach (var c in outer2D)
                        {
                            var cornerVertices = new List<Polyline2DVertex>
                        {
                            new Polyline2DVertex(c.X, c.Y, 0),
                            new Polyline2DVertex(c.X + offset, c.Y, 0),
                            new Polyline2DVertex(c.X + offset, c.Y + offset, 0),
                            new Polyline2DVertex(c.X, c.Y + offset, 0)
                        };
                            var cornerPoly = new Polyline2D(cornerVertices, true);
                            var hatch = new Hatch(HatchPattern.Solid, true);
                            hatch.BoundaryPaths.Add(new HatchBoundaryPath(new List<EntityObject> { cornerPoly }));
                            hatch.Layer = cornerLayer;
                            hatch.Color = new AciColor(7);

                            if (el.label == "Block")
                                dxf.Entities.Add(hatch);
                        }
                    }


                    if (createDimension)
                    {
                        // --- Wymiary ---
                        double dimOffset = 10.0;

                        // Szerokość (poziomy)
                        var wStart = outer2D[0];
                        var wEnd = outer2D[1];
                        var widthDim = new LinearDimension(wStart, wEnd, -dimOffset, 0.0, dimStyle)
                        {
                            Layer = dimLayer
                        };
                        dxf.Entities.Add(widthDim);

                        // Wysokość (pionowy)
                        var hStart = outer2D[1];
                        var hEnd = outer2D[2];
                        var heightDim = new LinearDimension(hStart, hEnd, dimOffset, 90.0, dimStyle)
                        {
                            Layer = dimLayer
                        };
                        dxf.Entities.Add(heightDim);
                    }

                }
            }

            dxf.Save(filePath);
        }

        // współrzędne dla poszczególnych widoków / perspektyw
        private List<Vector2> GenerateViewVertices(Element el, string view, double globalXMin, double globalXMax,
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

                default:
                    return new List<Vector2> { new Vector2(x1, y1), new Vector2(x2, y1), new Vector2(x2, y2), new Vector2(x1, y2) };
            }
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

                // brak nakładania → cały prostokąt zostaje
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



        private void Generate3D(List<Element> elements, string filePath)
        {
            var dxf = new DxfDocument();

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

        private void MainFrm_Load(object sender, EventArgs e)
        {
            StartProcessService sps = new StartProcessService();
            sps.TerminateExistingPreviousProcess(Path.GetFileNameWithoutExtension(Application.ExecutablePath));
        }
    }
}
