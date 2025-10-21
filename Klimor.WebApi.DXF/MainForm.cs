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
using System.Security.Cryptography;
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
    v 2.2.1
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

                        var isExtended = prodBox.Checked;
                        var norm = isExtended ? Norm.ISO_EXTENDED : Norm.ISO;

                        Generate2D(elements, $"{Path.GetFileNameWithoutExtension(ofd.FileName)}.dxf", isExtended, norm);
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

        private void SelectBlockUpChannel(List<Coordinates> elements)
        {            
            var upBlocks = elements
                .Where(e => e.label == ViewName.Up || (e.label.Contains("icon") && e.View == ViewName.Up))
                .ToList();

            if (upBlocks.Count == 0)
                return;

            var levels = upBlocks.Where(e => !e.label.Contains("icon"))
                .Select(e => e.y2)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            if (levels.Count > 1)
            {
                // bierzemy wszystkie poziomy poza najniższym
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = upBlocks
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                // usuwamy oryginały
                elements.RemoveAll(e => e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault() && (e.label == Lab.Up || e.label == Lab.Block) && e.View == ViewName.Up);
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && !e.label.Contains("icon") && (e.label == Lab.Up || e.label == Lab.Block) && e.View == ViewName.UpUp);
                
                // ikony
                levels = upBlocks
                    .Where(e => e.label.Contains("icon"))
                    .Select(e => e.y2)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();
                upperLevels = levels.Skip(1).ToList();

                upperWalls = upBlocks
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                var iconsUpUp = upperWalls
                    .Where(e => (e.label.Contains("icon") && (e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault())))
                    .ToList();
                foreach (var item in iconsUpUp)
                {
                    item.View = ViewName.UpUp;
                    item.additionalInfos.iconPosition = ViewName.UpUp;
                }
            }
        }

        private void SelectExternalElementsUpChannel(List<Coordinates> elements, string extrLabel)
        {
            // wyciągamy UP-y
            var upWalls = elements
                .Where(e => e.label == ViewName.Up || (e.label == extrLabel && e.View == ViewName.Up))
                .ToList();

            if (upWalls.Count == 0)
                return;

            // różne poziomy Y2
            var levels = upWalls.Where(e => e.label == extrLabel)
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

                // usuwamy oryginały
                elements.RemoveAll(e => (e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault()) && (e.label == extrLabel) && e.View == ViewName.Up);
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && (e.label == extrLabel) && e.View == ViewName.UpUp);                
            }
        }

        private void SelectExternalElementsDownChannel(List<Coordinates> elements, string extrLabel)
        {
            var downEls = elements
                .Where(e => e.label == ViewName.Down || (e.label == extrLabel && e.View == ViewName.Down))
                .ToList();

            if (downEls.Count == 0)
                return;
            
            var levels = downEls
                .Select(e => e.y1)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            
            if (levels.Count > 1)
            {
                // zamiast levels.Count - 1 => C# 8 [^1]                
                var topLevel = levels[^1]; // najwyższy Y1
                var top2Level = levels.Count > 1 ? levels[^2] : topLevel; // drugi najwyższy (gdy jest)

                // bierzemy tylko ściany z najwyższego poziomu
                var topLevelWalls = downEls
                    .Where(e => e.y1.Equals(topLevel))
                    .ToList();

                if (levels.Count > 2)
                {
                    elements.RemoveAll(e => (e.y1 == topLevel || e.y1 == top2Level) && e.View == ViewName.Down && e.label == extrLabel);
                    elements.RemoveAll(e => e.y1 != topLevel && e.View == ViewName.DownUp && e.label == extrLabel);
                }
                else
                {
                    elements.RemoveAll(e => e.y1 == topLevel && e.View == ViewName.Down && e.label == extrLabel);
                    elements.RemoveAll(e => e.y1 != topLevel && e.View == ViewName.DownUp && e.label == extrLabel);
                }                    
            }
        }

        private void SelectBlockDownChannel(List<Coordinates> elements)
        {            
            var downBlocks = elements
                .Where(e => (e.label == Lab.Down_Wall || e.label == Lab.Down_DrainTray) || (e.View == ViewName.Down && e.label == Lab.Block))
                .ToList();

            if (downBlocks.Count == 0)
                return;
            
            var levels = downBlocks
                .Select(e => e.y1)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            
            if (levels.Count > 1)
            {
                // zamiast levels.Count - 1 => C# 8 [^1]                
                var topLevel = levels[^1]; // najwyższy Y1

                // bierzemy tylko ściany z najwyższego poziomu
                var topLevelWalls = downBlocks
                    .Where(e => e.y1.Equals(topLevel))
                    .ToList();

                elements.RemoveAll(e => e.y1 == topLevel && e.View == ViewName.Down && e.label == Lab.Block);
                elements.RemoveAll(e => e.y1 != topLevel && e.View == ViewName.DownUp && e.label == Lab.Block);
            }
        }

        private void SelectFunctionUpChannel(List<Coordinates> elements)
        {            
            var upFunctions = elements
                .Where(e => e.label == Lab.Function && e.View == ViewName.Up)
                .ToList();

            if (upFunctions.Count == 0)
                return;
            
            var levels = upFunctions
                .Select(e => e.y2)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            // jeśli więcej niż jeden poziom, bierzemy najwyższy
            if (levels.Count > 1)
            {
                // bierzemy wszystkie poziomy poza najniższym
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = upFunctions
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                // usuwamy oryginały
                elements.RemoveAll(e => (e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault()) && e.View == ViewName.Up && (e.label == Lab.Function) && e.View == ViewName.Up);
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && (e.label == Lab.Function) && e.View == ViewName.UpUp);                            
            }
        }

        private void SelectFunctionDownChannel(List<Coordinates> elements)
        {
            var downWalls = elements
                .Where(e => e.label == Lab.Function && e.View == ViewName.Down)
                .ToList();

            if (downWalls.Count == 0)
                return;
            
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

                // bierzemy tylko fnc z najwyższego poziomu
                var topLevelWalls = downWalls
                    .Where(e => e.y1.Equals(topLevel))
                    .ToList();

                elements.RemoveAll(e => e.y1 == topLevel && e.View == ViewName.Down && e.label == Lab.Function);
                elements.RemoveAll(e => e.y1 != topLevel && e.View == ViewName.DownUp && e.label == Lab.Function);
            }
        }

        // przypisanie connectorów do funkcji
        private void AssignDrainTrayConnectorsToFunctions(List<Coordinates> elements)
        {
            var filtered = elements
                .Where(e => e.label != "Connector")
                .Concat(elements
                    .Where(e => e.label == "Connector")
                    .DistinctBy(e => new { e.x1, e.x2, e.y1, e.y2, e.z1, e.z2, e.PositionUp, e.PositionDown }))
                .ToList();

            elements.Clear();
            elements.AddRange(filtered);

            var connectors = elements.Where(e => e.label == Lab.Connector).ToList();
            var functions = elements.Where(e => e.label == Lab.Function
                && (e.View == ViewName.UpUp || e.View == ViewName.DownUp
                    || e.View == ViewName.Up || e.View == ViewName.Down)).ToList();

            foreach (var connector in connectors)
            {
                var function = functions.FirstOrDefault(f => f.PositionUp == connector.PositionUp
                                                          && f.PositionDown == connector.PositionDown);
                if (function != null)
                {
                    connector.View = function.View;
                }
            }

            // tu zarządzamy CONNECTORAMI - kopiujemy do pozostałych widoków
            foreach (var view in Views.Except("Frame", "FrameUp", "Roof", "RoofUp", "RightFront", "LeftFront", "DownUp", "UpUp", "Back", "Up"))
            {                
                foreach (var c in connectors)
                {
                    elements.Add(new Coordinates
                    {
                        View = view.Name,
                        label = c.label,
                        type = c.type,
                        x1 = c.x1,
                        x2 = c.x2,
                        y1 = c.y1,
                        y2 = c.y2,
                        z1 = c.z1,
                        z2 = c.z2,
                        PositionUp = c.PositionUp,
                        PositionDown = c.PositionDown,
                        posUpDown = c.posUpDown,
                        additionalInfos = c.additionalInfos,
                    });
                }
            }
        }

        private void AssignExternalElementsToFunctions(List<Coordinates> elements)
        {
            var functions = elements.Where(e => e.label == Lab.Function
                                     && (e.View == ViewName.UpUp || e.View == ViewName.DownUp
                                     || e.View == ViewName.Up || e.View == ViewName.Down)).ToList();

            foreach (var ex in elements.Where(e => e.label == Lab.AD).ToList())
            {
                var function = functions.Where(f => f.PositionUp == ex.PositionUp
                                                        && f.PositionDown == ex.PositionDown
                                                        && f.View == ex.View);
                foreach (var fnc in function)
                {
                    ex.View = fnc.View;
                }
            }

            foreach (var ex in elements.Where(e => e.label == Lab.FC).ToList())
            {
                var function = functions.Where(f => f.PositionUp == ex.PositionUp
                                                        && f.PositionDown == ex.PositionDown
                                                        && f.View == ex.View);
                foreach (var fnc in function)
                {
                    ex.View = fnc.View;
                }
            }

            foreach (var ex in elements.Where(e => e.label == Lab.INTK).ToList())
            {
                var function = functions.Where(f => f.PositionUp == ex.PositionUp
                                                        && f.PositionDown == ex.PositionDown
                                                        && f.View == ex.View);
                foreach (var fnc in function)
                {
                    ex.View = fnc.View;
                }
            }
        }

        private void SelectWallUpChannel(List<Coordinates> elements)
        {            
            var upWalls = elements
                .Where(e => e.label == ViewName.Up && (e.type == Lab.Wall && e.View == ViewName.Up))
                .ToList();

            if (upWalls.Count == 0)
                return;
            
            var levels = upWalls
                .Select(e => e.y2)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            
            if (levels.Count > 1)
            {
                // bierzemy wszystkie poziomy poza najniższym
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = upWalls
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                // usuwamy oryginały
                elements.RemoveAll(e => e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault() && (e.label == Lab.Up && e.type == Lab.Wall) && e.View == ViewName.Up);
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && (e.label == Lab.Up && e.type == Lab.Wall) && e.View == ViewName.UpUp);                
            }
        }

        private void SelectWallDownChannel(List<Coordinates> elements)
        {
            // wyciągamy downy
            Func<Coordinates, bool> downCondition = e =>
                    (e.label is (Lab.Down_Wall or Lab.Down_DrainTray or Lab.Down_Div)) &&
                    (e.type is (Lab.Wall or Lab.Down_DrainTray or Lab.DrainTray or Lab.Down_Div) ||
                     e.View == ViewName.Down);

            var downWalls = elements.Where(downCondition).ToList();

            if (downWalls.Count == 0)
                return;
            
            var levels = downWalls
                .Select(e => e.y1)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            
            if (levels.Count > 1)
            {
                // zamiast levels.Count - 1 => C# 8 [^1]                
                var topLevel = levels[^1]; // najwyższy Y1

                // bierzemy tylko ściany z najwyższego poziomu
                var topLevelWalls = downWalls
                    .Where(e => e.y1.Equals(topLevel))
                    .ToList();

                elements.RemoveAll(e => e.y1 == topLevel && e.View == ViewName.Down && downCondition(e));
                elements.RemoveAll(e => e.y1 != topLevel && e.View == ViewName.DownUp && downCondition(e));
            }
        }

        private void SelectFrameUpChannel(List<Coordinates> elements)
        {            
            var frames = elements
                .Where(e => e.label == Lab.Frame && e.View == ViewName.Frame)
                .ToList();

            if (frames.Count == 0)
                return;
            
            var levels = frames
                .Select(e => e.y2)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            
            if (levels.Count > 1)
            {
                // bierzemy wszystkie poziomy poza najniższym
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = frames
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                // usuwamy oryginały
                elements.RemoveAll(e => e.y2 == upperLevels.FirstOrDefault() && (e.label == Lab.Frame) && e.View == ViewName.Frame);
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && (e.label == Lab.Frame) && e.View == ViewName.FrameUp);
            }
        }

        private void SelectRoofUpChannel(List<Coordinates> elements)
        {            
            var roofs = elements
                .Where(e => e.label == ViewName.Roof && (e.View == ViewName.Roof))
                .ToList();

            if (roofs.Count == 0)
                return;
            
            var levels = roofs
                .Select(e => e.y2)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            // jeśli więcej niż jeden poziom, bierzemy najwyższy
            if (levels.Count > 1)
            {
                // bierzemy wszystkie poziomy poza najniższym
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = roofs
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                // usuwamy oryginały
                elements.RemoveAll(e => e.y2 == upperLevels.FirstOrDefault() && (e.label == Lab.Roof) && e.View == ViewName.Roof);
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && e.label == Lab.Roof && e.View == ViewName.RoofUp);
            }
        }

        private void MapElementsToViews(List<Coordinates> elements, bool extended)
        {
            foreach (var el in elements.ToList())
            {
                if (el.label == "FrontRight")
                {
                    el.label = "RightFront";
                }
                if (el.label == "FrontLeft")
                {
                    el.label = "LeftFront";
                }
            }
            var views = Views.Except("Frame", "Roof", "Connector");
            foreach (var el in elements.ToList())
            {
                foreach (var vw in views)
                {
                    if ((el.label == vw.Name || el.label == Lab.Block) && el.type != Lab.Wall)
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }
                    
                    if (el.label == Lab.Function)
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }

                    if (el.label is (Lab.AD or Lab.FC or Lab.INTK))
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }

                    if (el.label == Lab.Up && (el.type == Lab.Wall || el.type == Lab.Div) && vw.Name is not (ViewName.Down or ViewName.DownUp))
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }

                    if (el.label is (Lab.Down_Wall or Lab.Down_Div or Lab.Down_DrainTray) && (el.type is Lab.Wall or Lab.Div or Lab.Down_DrainTray or Lab.DrainTray) && vw.Name is not (ViewName.Up or ViewName.UpUp))
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }

                    if (el.label is Lab.Back && el.type is Lab.Wall)
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }

                    if (el.label is Lab.Frame)
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }

                    if ((el.label is (ViewName.LeftFront or ViewName.RightFront)) && el.type == Lab.Wall && (vw.Name is (ViewName.LeftFront or ViewName.RightFront)))
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }
                }
            }

            views = Views.Select("Frame", "FrameUp", "Roof", "RoofUp");
            foreach (var el in elements.ToList())
            {
                foreach (var vw in views)
                {                    
                    if (el.label is Lab.Frame && vw.Name is (ViewName.Frame or ViewName.FrameUp))
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }

                    if (el.label is Lab.Roof && vw.Name is (ViewName.Roof or ViewName.RoofUp))
                    {
                        var addBlock = new Coordinates
                        {
                            View = vw.Name,
                            label = el.label,
                            type = el.type,
                            x1 = el.x1,
                            x2 = el.x2,
                            y1 = el.y1,
                            y2 = el.y2,
                            z1 = el.z1,
                            z2 = el.z2,
                            PositionUp = el.PositionUp,
                            PositionDown = el.PositionDown,
                            posUpDown = el.posUpDown,
                            additionalInfos = el.additionalInfos,
                        };
                        elements.Add(addBlock);
                    }
                }
            }
            
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Operational or Lab.Back) && e.label != ViewName.RightFront);
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Block or Lab.Function));            
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && (e.label is Lab.AD or Lab.FC or Lab.INTK));            
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label == Lab.Up && (e.type == Lab.Wall || e.type == Lab.Div));
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Down_Wall or Lab.Down_Div or Lab.Down_DrainTray or Lab.Up) && (e.type is Lab.Wall or Lab.Div or Lab.Down_DrainTray or Lab.Down_DrainTray));
            elements.RemoveAll(e => e.type is (Lab.Div or Lab.Wall) && e.label != e.View && e.label is not (Lab.Down_Wall or Lab.Down_Div or Lab.Down_DrainTray or Lab.Up));
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Frame or Lab.Roof));
            elements.RemoveAll(e => e.label is (Lab.Frame or Lab.Roof) && e.View is (ViewName.Down or ViewName.DownUp or ViewName.Up or ViewName.UpUp));
            elements.RemoveAll(e => e.label is Lab.Frame && e.View is (ViewName.Down or ViewName.DownUp or ViewName.Up or ViewName.UpUp));            
            elements.RemoveAll(e => e.label is Lab.Roof && e.View is not (ViewName.Roof or ViewName.RoofUp));
            elements.RemoveAll(e => e.label is Lab.Frame && e.View is (ViewName.Roof or ViewName.RoofUp));
            elements.RemoveAll(e => e.label is (ViewName.Up or Lab.Down_Wall) && e.View is (ViewName.LeftFront or ViewName.RightFront));

            // ikony
            var icons = elements.Where(e => e.label.Contains("icon")).ToList();
            foreach (var icon in icons)
            {
                icon.View = icon.additionalInfos.iconPosition;
            }
        }

        private void Generate2D(List<Coordinates> elements, string fileOutput, bool isExtended, Norm norm)
        {            
            dxf2D.isExtended = isExtended;

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
            Views.ApplyNormOnGrid(norm, grid);
            var drawer = new GridDrawer(dxf);
            drawer.Draw(grid);

            // odsuwanie żeby w komórce GRID były na środku
            if (norm == Norm.ISO || norm == Norm.ISO_EXTENDED)
            {
                Views.SetView(ViewName.LeftFront, Views.LeftFront.XOffset + (Views.LeftFront.XOffset / 2) - ((int)Views.AhuWidth / 2), Views.LeftFront.YOffset);
                Views.SetView(ViewName.RightFront, Views.RightFront.XOffset - (Views.RightFront.XOffset / 2) - ((int)Views.AhuWidth / 2), Views.RightFront.YOffset);
            }
            
            //var vall = Views.All;

            void DrawBlocks()
            {
                var layer = dxf.Layers.Add(new Layer(Lab.Block) { Color = AciColor.Default });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Block }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp));
            }

            void GenerateWalls()
            {
                var layer = dxf.Layers.Add(new Layer("Walls") { Color = AciColor.Default });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Operational, Lab.Up, Lab.Down, Lab.Down_DrainTray, Lab.Down_Wall, Lab.Back, ViewName.LeftFront, ViewName.RightFront }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void GenerateWallsDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Walls_dimension") { Color = AciColor.Default });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Operational, Lab.Up, Lab.Down, Lab.Down_DrainTray, Lab.Down_Wall, Lab.Back }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof));
            }

            void DrawBlockDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Block_dimensions") { Color = AciColor.DarkGray });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Block }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp));
            }

            void DrawFunctionsWithIcons(bool production)
            {
                var upOffset = Views.Up.YOffset;
                var upUpOffset = Views.UpUp.YOffset;
                var iconsList = icons.Blocks.ToList();
                var layer = dxf.Layers.Add(new Layer(Lab.Function) { Color = new AciColor(4) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Function }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp));

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

                            case ViewName.UpUp when production:
                                var insertIconUpUp = new Insert(insertIcon)
                                {
                                    Position = new Vector3(icon.x1, icon.z1 + upUpOffset, 0),
                                    Layer = layer,
                                };
                                if (!isExhaust && icon.additionalInfos.sName == "VF")
                                {
                                    insertIconUpUp.Position = new Vector3(icon.x1 + (icon.x2 - icon.x1), icon.z1 + upUpOffset, 0);
                                    insertIconUpUp.Scale = new Vector3(-1, 1, 1);
                                }

                                dxf.Entities.Add(insertIconUpUp);
                                break;

                            default:
                                break;
                        }
                    }
                }
            }

            void DrawFunctionsDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Function_dimensions") { Color = AciColor.Green });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Function }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp));
            }

            void DrawExternalElements()
            {
                var layer = dxf.Layers.Add(new Layer("ExternalElements") { Color = AciColor.Magenta });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Hole, Lab.AD, Lab.FC, Lab.INTK, Lab.Connector }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp));

                var layerDim = dxf.Layers.Add(new Layer("ExternalElements_dimensions") { Color = AciColor.Cyan });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Hole, Lab.AD, Lab.FC, Lab.INTK, Lab.Connector }, true, false, layerDim, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp));
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
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Frame, Lab.FrameUp }, false, true, layerFrame, textLayer, Views.Except(ViewName.Up, ViewName.Down, ViewName.Roof));
            }

            void GenerateFrameDimensions()
            {
                var layerFrameDim = dxf.Layers.Add(new Layer("Frame_dimensions") { Color = AciColor.Blue });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Frame }, true, false, layerFrameDim, textLayer, Views.Except(ViewName.Up, ViewName.Down, ViewName.Roof));
            }

            void GenerateRoof()
            {
                var layerRoof = dxf.Layers.Add(new Layer("Roof") { Color = new AciColor(9) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Roof }, false, true, layerRoof, textLayer, Views.Select(ViewName.Roof, ViewName.RoofUp));
            }

            void GenerateRoofDimensions()
            {
                var layerRoofDim = dxf.Layers.Add(new Layer("Roof_dimensions") { Color = new AciColor(9) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Roof }, true, false, layerRoofDim, textLayer, Views.Select(ViewName.Roof, ViewName.RoofUp));
            }
            
            // rozszerzanie listy elementów o widoki globalne
            MapElementsToViews(elements, isExtended);
            PrepareElementsToMode(elements, isExtended);
            if (!isExtended)
            {
                if (!elements.Any(e => e.label == Lab.Roof))
                    Views.Roof.Visibility = false;
                if (!elements.Any(e => e.label == Lab.Frame))
                    Views.Frame.Visibility = false;
                Views.UpUp.Visibility = false;
                Views.DownUp.Visibility = false;
                Views.FrameUp.Visibility = false;
                Views.RoofUp.Visibility = false;
            }

            // przypisywanie DownUp i UpUp, wybór górnych i dolnych kanałów
            if (isExtended)
            {
                SelectBlockUpChannel(elements);
                SelectBlockDownChannel(elements);
                SelectFunctionDownChannel(elements);
                SelectFunctionUpChannel(elements);

                SelectExternalElementsUpChannel(elements, Lab.AD);
                SelectExternalElementsUpChannel(elements, Lab.FC);
                SelectExternalElementsUpChannel(elements, Lab.INTK);
                SelectExternalElementsDownChannel(elements, Lab.AD);
                SelectExternalElementsDownChannel(elements, Lab.FC);
                SelectExternalElementsDownChannel(elements, Lab.INTK);
                SelectFrameUpChannel(elements);
                SelectRoofUpChannel(elements);                
                SelectWallUpChannel(elements);
                SelectWallDownChannel(elements);
            }

            // powiązanie connectorów z funkcjami
            AssignDrainTrayConnectorsToFunctions(elements);

            // powiązanie external elements z funkcjami
            AssignExternalElementsToFunctions(elements);

            // ustawianie widoczności elementów (kolejność ma znaczenie)
            SetElementsVisibility(elements, [Lab.Frame, Lab.FrameUp], Views.Except(ViewName.Frame) , null, false);
            SetElementsVisibility(elements, [Lab.Frame], Views.Select(ViewName.RightFront), true, true);

            if (true)
            {
                DrawBlocks();
                DrawBlockDimensions();
                DrawFunctionsWithIcons(isExtended);
                DrawFunctionsDimensions();
                DrawExternalElements();
                GenerateWalls();
                GenerateWallsDimensions();
                GenerateFrame();
                GenerateFrameDimensions();
                GenerateRoof();
                GenerateRoofDimensions();
                GeneratePorthole();
                GeneratePortholeDimension();
            }                        

            // do zrobienia
            //GeneratePorthole();
            //GeneratePortholeDimension();
            //GenerateSwitchbox();
            //GenerateSwitchboxDimension();

            //if (!advanced2D)
            //{
            //    GenerateFunctionsWithIcons();
            //    GenerateFunctionsDimensions();
            //    GenerateExternalElements();
            //}

            //if (advanced2D)
            //{
            //    GenerateWalls();
            //    GenerateWallsDimensions();
            //    GenerateExternalElements();
            //    GenerateFrame();
            //    GenerateFrameDimensions();
            //    GenerateRoof();
            //    GenerateRoofDimensions();
            //    GeneratePorthole();
            //    GeneratePortholeDimension();
            //    GenerateSwitchbox();
            //    GenerateSwitchboxDimension();
            //}
            PrepareLayersToMode(dxf, isExtended);
            dxf.Save(fileOutput);
        }

        private void SetElementsVisibility(List<Coordinates> elements, IEnumerable<string> labels, IEnumerable<ViewElement> views, bool? show, bool? show_dimension)
        {
            var labelSet = new HashSet<string>(labels);
            var viewSet = new HashSet<string>(views.Select(v => v.Name));

            foreach (var el in elements)
            {
                if (labelSet.Contains(el.label) && viewSet.Contains(el.View))
                {
                    if (show.HasValue)
                        el.Show = show.Value;

                    if (show_dimension.HasValue)
                        el.ShowDimension = show_dimension.Value;
                }
            }
        }

        private void SetElementsVisibility(
            List<Coordinates> elements,
            string label,
            IEnumerable<ViewElement> views,
            bool show,
            bool show_dimension)
            => SetElementsVisibility(elements, new[] { label }, views, show, show_dimension);

        private void PrepareElementsToMode(List<Coordinates> elements, bool isExtended)
        {
            if (!isExtended)
            {
                foreach (var el in elements.ToList())
                {
                    if (!(el.type.Contains("Removable") || el.type.Contains("Door")) &&
                        (el.label is not (Lab.Block or Lab.Function) && !el.label.Contains("icon")) &&
                        !Lab.ExternalElements.Any(l => l == el.label))
                    {
                        elements.Remove(el);
                    }
                }
            }            
        }

        private void PrepareLayersToMode(DxfDocument dxf, bool isExtended)
        {
            if (!isExtended)
            {
                foreach (var layer in dxf.Layers)
                {
                    layer.IsVisible = layer.Name switch
                    {
                        "Function_dimensions" or
                        "Walls_dimensions" => false,
                        _ => layer.IsVisible
                    };
                }
            }            
        }


        private void MainFrm_Load(object sender, EventArgs e)
        {
            var autoload = false;
            StartProcessService sps = new StartProcessService();
            sps.TerminateExistingPreviousProcess(Path.GetFileNameWithoutExtension(Application.ExecutablePath));

            if (autoload)
            {
                var path = @"D:\\DXFApp\\DXF\\Ogromna_Debug.json";
                string json = File.ReadAllText(path);
                List<Coordinates> elements = JsonSerializer.Deserialize<List<Coordinates>>(json);

                elements = elements.Where(e => !string.IsNullOrWhiteSpace(e.label)).ToList();

                Generate2D(elements, $"{Path.GetFileNameWithoutExtension(path)}.dxf", true, Norm.ISO);
                var dwgPath = @"C:\Program Files\Autodesk\DWG TrueView 2026 - English\dwgviewr.exe";
                var dxfPath = Path.ChangeExtension(Path.GetFileNameWithoutExtension(path), ".dxf");

                Process.Start(new ProcessStartInfo(dwgPath, $"\"{dxfPath}\"")
                {
                    UseShellExecute = false
                });
                Close();
            }
        }
    }
}
