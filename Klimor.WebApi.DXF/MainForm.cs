using Klimor.WebApi.DXF.Consts;
using Klimor.WebApi.DXF.Development;
using Klimor.WebApi.DXF.Services;
using Klimor.WebApi.DXF.Structures;
using netDxf;
using netDxf.Blocks;
using netDxf.Collections;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Objects;
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
        ViewsList Views;
        CornerService cornerService;
        Dxf2DService dxf2D;
        Dxf3DService dxf3D;

        public MainFrm()
        {
            InitializeComponent();
            Views = new ViewsList();
            dxf2D = new Dxf2DService(Views);
            dxf3D = new Dxf3DService();
        }

        private void btnOpenJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                ofd.Title = "Wybierz plik JSON";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    //try
                    {
                        string json = File.ReadAllText(ofd.FileName);
                        List<Coordinates> elements = JsonSerializer.Deserialize<List<Coordinates>>(json);

                        elements = elements.Where(e => !string.IsNullOrWhiteSpace(e.label)).ToList();

                        Generate2D(elements, $"{Path.GetFileNameWithoutExtension(ofd.FileName)}.dxf", true, Norm.ISO);
                        //GenerateViews(elements, "output2D.dxf");
                        dxf3D.Generate3D(elements, "output3D.dxf");

                        //MessageBox.Show("Pliki DXF zostały wygenerowane.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    //catch (Exception ex)
                    //{
                     //   MessageBox.Show("Błąd podczas wczytywania: " + ex.Message);
                   // }
                }
            }
            if (!Debugger.IsAttached)               
                Application.Exit(); 
        }

        private void AddElementsForUpChannel(List<Coordinates> elements)
        {
            // wyciągamy UP-y
            var upWalls = elements
                .Where(e => e.label == ViewName.Up)
                .ToList();

            if (upWalls.Count == 0)
                return;

            // różne poziomy Y2
            var levels = upWalls
                .Select(e => e.y2)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            // jeśli więcej niż jeden poziom, bierzemy najwyższy
            if (levels.Count > 1)
            {
                // bierzemy wszystkie poziomy poza najniższym
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = upWalls
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                elements.AddRange(upperWalls.Select(w => new Coordinates
                {
                    label = ViewName.UpUp,
                    type = w.type,
                    x1 = w.x1,
                    x2 = w.x2,
                    y1 = w.y1,
                    y2 = w.y2,
                    z1 = w.z1,
                    z2 = w.z2,
                    posUpDown = "up"
                }));
            }
        }

        private void AddElementsForDownChannel(List<Coordinates> elements)
        {
            // wyciągamy UP-y
            var downWalls = elements
                .Where(e => e.label == Lab.Down_Wall || e.label == Lab.Down_DrainTray)
                .ToList();

            if (downWalls.Count == 0)
                return;

            // różne poziomy Y2
            var levels = downWalls
                .Select(e => e.y1)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            // jeśli więcej niż jeden poziom, bierzemy najwyższy
            if (levels.Count > 1)
            {
                // zamiast levels.Count - 1 => C# 8 [^1]
                var topLevel = levels[^1]; // najwyższy Y1

                // bierzemy tylko ściany z najwyższego poziomu
                var topLevelWalls = downWalls
                    .Where(e => e.y1.Equals(topLevel))
                    .ToList();

                // dokładamy kopie z label = UpUp
                elements.AddRange(topLevelWalls.Select(w => new Coordinates
                {
                    label = ViewName.DownUp,
                    type = w.type,
                    x1 = w.x1,
                    x2 = w.x2,
                    y1 = w.y1,
                    y2 = w.y2,
                    z1 = w.z1,
                    z2 = w.z2,
                    posUpDown = "DownUp"
                }));
            }
        }

        private void Generate2D(List<Coordinates> elements, string fileOutput, bool advanced2D, Norm norm)
        {
            Views.AhuLength = elements.Where(el => el.label == Lab.Block).Max(e => e.x2);
            Views.AhuHeight = elements.Where(el => el.label == Lab.Block).Max(e => e.y2);
            Views.AhuWidth = elements.Where(el => el.label == Lab.Block).Max(e => e.z2);

            var icons = DxfDocument.Load("BLOCKS.dxf");
            var dxf = new DxfDocument();
            var cornerLayer = new Layer("CornerFill") { Color = new AciColor(7) };
            var textLayer = new Layer("Text_Views") { Color = AciColor.Blue };

            dxf2D.globalXMin = elements.Min(e => e.x1);
            dxf2D.globalXMax = elements.Max(e => e.x2);
            dxf2D.globalYMin = elements.Min(e => e.y1);
            dxf2D.globalYMax = elements.Max(e => e.y2);
            dxf2D.globalZMin = elements.Min(e => e.z1);
            dxf2D.globalZMax = elements.Max(e => e.z2);

            Views.ApplyNorm(norm);
            Views.SetWaterMark("EVO");

            var grid = new ViewGrid(columns: 5, rows: 10, cellWidth: (int)Views.AhuLength, cellHeight: 1000 + (int)Views.AhuHeight);
            grid.AlignCellToPoint(col: 1, row: 5, worldX: 0, worldY: 0);
            
            // Użycie presetów siatkowych:
            Views.ApplyNormOnGrid(Norm.ISO, grid);            
            var drawer = new GridDrawer(dxf);
            drawer.Draw(grid);

            // odsuwanie żeby w komórce GRID były na środku
            if (norm == Norm.ISO)
            {
                Views.SetView(ViewName.LeftFront, Views.LeftFront.XOffset + (Views.LeftFront.XOffset / 2) - ((int)Views.AhuWidth / 2), Views.LeftFront.YOffset);
                Views.SetView(ViewName.RightFront, Views.RightFront.XOffset - (Views.RightFront.XOffset / 2) - ((int)Views.AhuWidth / 2), Views.RightFront.YOffset);
            }                        

            void GenerateBlocks()
            {
                var layer = dxf.Layers.Add(new Layer(Lab.Block) { Color = AciColor.Default });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Block }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GenerateWalls()
            {
                var layer = dxf.Layers.Add(new Layer("Walls") { Color = AciColor.Default });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Operational, Lab.Up, Lab.Down, Lab.Down_DrainTray, Lab.Down_Wall, Lab.Back}, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GenerateWallsDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Walls_dimension") { Color = AciColor.Default });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Operational, Lab.Up, Lab.Down, Lab.Down_DrainTray, Lab.Down_Wall, Lab.Back}, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GenerateBlockDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Block_dimensions") { Color = AciColor.DarkGray });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Block }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GenerateFunctionsWithIcons()
            {
                var upOffset = Views.Up.YOffset; //views.FirstOrDefault(v => v.name == ViewName.Up).yOffset;
                var iconsList = icons.Blocks.ToList();
                var layer = dxf.Layers.Add(new Layer(Lab.Function) { Color = new AciColor(4) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Function }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));

                // dodawanie ikon
                var sName = string.Empty;
                var distinctList = elements.DistinctBy(e => (e.posUpDown, e.x1, e.y1)).Where(i => i.label.Contains("icon") && i.additionalInfos != null).ToList();
                foreach (var icon in distinctList)
                {
                    sName = icon.additionalInfos.iconName;
                    bool isExhaust = icon.additionalInfos.airPath.ToLower() == "exhaust";
                    if (Dxf2DService.IconMap.ContainsKey(sName!))
                    {
                        var insertIcon = iconsList.FirstOrDefault(b => b.Name.Equals(sName, StringComparison.OrdinalIgnoreCase));
                        switch (icon.additionalInfos.iconPosition)
                        {
                            case ViewName.Operational:
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

                                if (icon.additionalInfos.iconPosition != Lab.Back)
                                    dxf.Entities.Add(insertIconOperational);
                                break;

                            case ViewName.Up:
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
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Function }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GenerateExternalElements()
            {
                var layer = dxf.Layers.Add(new Layer("ExternalElements") { Color = AciColor.Magenta });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Hole, Lab.AD, Lab.FC, Lab.INTK, Lab.Connector }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));

                var layerDim = dxf.Layers.Add(new Layer("ExternalElements_dimensions") { Color = AciColor.Cyan });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Hole, Lab.AD, Lab.FC, Lab.INTK, Lab.Connector }, true, false, layerDim, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));                                
            }                                    

            void GeneratePorthole()
            {
                var layer = dxf.Layers.Add(new Layer("Porthole") { Color = AciColor.Magenta });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Porthole }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GeneratePortholeDimension()
            {
                var layerDim = dxf.Layers.Add(new Layer("Porthole_dimensions") { Color = AciColor.Magenta });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Porthole }, true, false, layerDim, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GenerateSwitchbox()
            {
                var layer = dxf.Layers.Add(new Layer("Switchbox") { Color = new AciColor(4) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Switchbox }, false, true, layer, textLayer, Views.Select(ViewName.Operational, ViewName.Back));
            }

            void GenerateSwitchboxDimension()
            {
                var layerDim = dxf.Layers.Add(new Layer("Switchbox_dimensions") { Color = new AciColor(4) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Switchbox }, true, false, layerDim, textLayer, Views.Select(ViewName.Operational, ViewName.Back));
            }

            void GenerateFrame()
            {                
                var layerFrame = dxf.Layers.Add(new Layer("Frame") { Color = AciColor.Blue });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Frame, Lab.Roof }, false, true, layerFrame, textLayer, Views.Except(ViewName.Up, ViewName.Down, ViewName.Roof));                
            }

            void GenerateFrameDimensions()
            {
                var layerFrameDim = dxf.Layers.Add(new Layer("Frame_dimensions") { Color = AciColor.Blue });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Frame, Lab.Roof }, true, false, layerFrameDim, textLayer, Views.Except(ViewName.Up, ViewName.Down, ViewName.Roof));
            }

            void GenerateRoof()
            {
                var layerRoof = dxf.Layers.Add(new Layer("Roof") { Color = new AciColor(9) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Roof }, false, true, layerRoof, textLayer, Views.Select(ViewName.Roof));                
            }

            void GenerateRoofDimensions()
            {
                var layerRoofDim = dxf.Layers.Add(new Layer("Roof_dimensions") { Color = new AciColor(9) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Roof }, true, false, layerRoofDim, textLayer, Views.Select(ViewName.Roof));
            }

            // rozdzielenie dla widoków UpUp i DownUp
            AddElementsForUpChannel(elements);
            AddElementsForDownChannel(elements);

            GenerateBlocks();
            GenerateBlockDimensions();

            if (!advanced2D)
            {
                GenerateFunctionsWithIcons();
                GenerateFunctionsDimensions();
                GenerateExternalElements();
            }

            if (advanced2D)
            {
                GenerateWalls();
                GenerateWallsDimensions();
                GenerateExternalElements();
                GenerateFrame();
                GenerateFrameDimensions();
                GenerateRoof();
                GenerateRoofDimensions();
                GeneratePorthole();
                GeneratePortholeDimension();
                GenerateSwitchbox();
                GenerateSwitchboxDimension();
            }
            
            dxf.Save(fileOutput);
        }                

        private void MainFrm_Load(object sender, EventArgs e)
        {
            StartProcessService sps = new StartProcessService();
            sps.TerminateExistingPreviousProcess(Path.GetFileNameWithoutExtension(Application.ExecutablePath));
        }
    }
}
