using Klimor.WebApi.DXF.Consts;
using Klimor.WebApi.DXF.Development;
using Klimor.WebApi.DXF.Services;
using Klimor.WebApi.DXF.Structures;
using Microsoft.Win32;
using netDxf;
using netDxf.Blocks;
using netDxf.Collections;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Objects;
using netDxf.Tables;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
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
    v 2.2.3
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
                        dxf3D.Generate3D(elements, $"{Path.GetFileNameWithoutExtension(ofd.FileName)}_3D.dxf");

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

        public void IconRotation_CorrectXY(List<Coordinates> coordinates)
        {
            var dbg = coordinates.Where(e => e.label.Contains("icon")).ToList();
            var allIcons = coordinates.DistinctBy(e => (e.posUpDown, e.x1, e.y1, e.z1)).Where(i => i.label.Contains("icon") && i.additionalInfos != null).ToList();
            foreach (var i in allIcons)
            {
                var ir = i.additionalInfos.iconRotation;
                if (ir != 0)
                {
                    var lenX = i.x2 - i.x1;
                    var lenY = i.y2 - i.y1;
                    var lenZ = i.z2 - i.z1;

                    switch (i.View)
                    {
                        case ViewName.Operational:
                            if (ir == 90 || ir == 180)
                            {
                                i.y1 += lenY;
                                i.y2 += lenY;
                                i.x1 += lenX;
                                i.x2 += lenX;
                            }
                            else
                            {
                                if (ir == 270)
                                {
                                    i.x1 += lenX;
                                    i.x2 += lenX;
                                }
                            }
                            break;
                        case ViewName.Back:
                            if (ir == 90 || ir == 180)
                            {
                                i.y1 += lenY;
                                i.y2 += lenY;
                                //i.x1 -= lenX;
                                //i.x2 -= lenX;
                            }

                            break;
                        case ViewName.Up:
                        case ViewName.UpUp:
                            if (ir > 0)
                            {
                                i.z1 += lenZ;
                                i.z2 += lenZ;
                                i.x1 += lenX;
                                i.x2 += lenX;
                            }
                            else
                            {
                                i.x1 -= lenX;
                                i.x2 -= lenX;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void SelectBlockUpChannel(List<Coordinates> elements)
        {
            var upBlocks = elements
                .Where(e => e.label == Lab.Block || (e.label.Contains("icon") && e.View == ViewName.Up))
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
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = upBlocks
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                // usuwamy oryginały
                elements.RemoveAll(e => e.y2 == upperLevels.FirstOrDefault() && (e.label == Lab.Block) && e.View == ViewName.Up);
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && !e.label.Contains("icon") && (e.label == Lab.Block) && e.View == ViewName.UpUp);

                // ikony jak wentylator jest pod drugim
                elements.RemoveAll(e => (e.y2 == levels.FirstOrDefault() + 1) && e.label.Contains("icon") && e.View == ViewName.Up);


                //elements.RemoveAll(e => (e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault()) && e.label == Lab.Up && e.View == ViewName.Up);
                //elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && !e.label.Contains("icon") && (e.label == Lab.Up) && e.View == ViewName.UpUp);

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
            else
            {
                Views.UpUp.Visibility = false;
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
                elements.RemoveAll(e => (e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault()) && (e.label == extrLabel) && e.View == ViewName.Up && e.y2 != upperLevels.FirstOrDefault());
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && (e.label == extrLabel) && e.View == ViewName.UpUp); // && e.y2 != upperLevels.FirstOrDefault() ??
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
            else
            {
                Views.DownUp.Visibility = false;
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

                var removeUps = elements.Where(e => (e.y2 == upperLevels.FirstOrDefault() || e.y2 == upperLevels.LastOrDefault()) && (e.label == Lab.Function) && e.View == ViewName.Up).ToList();

                // pokazywał ikonę wentylatora Supply na Up mimo, że zakrywa go wentylator Exhaust na UpUp [75033]
                var removeUpUps = elements.Where(e => (e.y2 == levels.Take(1).FirstOrDefault() && (e.label == Lab.Function) && e.View == ViewName.UpUp) ||
                                  (e.label == Lab.Function) && e.View == ViewName.Up && e.y2 == levels.Take(1).LastOrDefault()).ToList();

                // usuwamy oryginały
                elements.RemoveAll(e => removeUps.Contains(e));
                elements.RemoveAll(e => removeUpUps.Contains(e));
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
            foreach (var view in Views.Except("Frame", "FrameUp", "Roof", "RoofUp", "LeftFront", "DownUp", "UpUp", "Back", "Up"))
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
            /*
             Przyjęte założenie: na górze Hatch-e pojawią się tylko te, które mają ten sam Y2 co Wall UpUp Y2
            */
            var upWalls = elements
                .Where(e => (e.label == ViewName.Up) && (e.type == Lab.Wall) && e.View == ViewName.Up)
                .ToList();

            if (upWalls.Count == 0)
                return;

            var levels = upWalls
                .Select(e => e.y2)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            var upUpHatches = elements.Where(a => a.label == Lab.Hatch && a.View == ViewName.UpUp).Select(a => new { a.x1, a.x2 }).ToList();

            //if (levels.Count > 1)
            //{
            //    // bierzemy wszystkie poziomy poza najniższym
            //    var upperLevels = levels.Skip(1).ToList();

            //    var upperWalls = upWalls
            //        .Where(e => upperLevels.Contains(e.y2))
            //        .ToList();                

            //    // czyszczenie UpUp
            //    elements.RemoveAll(e => (e.y2 == upperLevels.FirstOrDefault()) && (e.label == Lab.Up || e.label == Lab.Hatch) && (e.type == Lab.Wall || e.type == Lab.Div || e.type.Contains("Removable")) && e.View == ViewName.Up);
            //    // czyszczenie Up
            //    elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && (e.label == Lab.Up || e.label == Lab.Hatch) && (e.type == Lab.Wall || e.type == Lab.Div || e.type.Contains("Removable")) && e.View == ViewName.UpUp);
            //}

            if (levels.Count > 1)
            {
                // bierzemy wszystkie poziomy poza najniższym
                var upperLevels = levels.Skip(1).ToList();

                var upperWalls = upWalls
                    .Where(e => upperLevels.Contains(e.y2))
                    .ToList();

                // czyszczenie UpUp
                elements.RemoveAll(e => (e.y2 == upperLevels.FirstOrDefault()) && (e.label == Lab.Up) && (e.type == Lab.Wall || e.type == Lab.Div || e.type.Contains("Removable")) && e.View == ViewName.Up);
                //elements.RemoveAll(e => (e.y2 == upperLevels.FirstOrDefault()) && (e.label == Lab.Hatch) && (e.type == Lab.Wall || e.type.Contains("Removable")) && e.View == ViewName.Up);

                // czyszczenie Up
                elements.RemoveAll(e => e.y2 == levels.Take(1).FirstOrDefault() && (e.label == Lab.Up) && (e.type == Lab.Wall || e.type == Lab.Div || e.type.Contains("Removable")) && e.View == ViewName.UpUp);
            }

            // usuwanie duplikatów Hatchy na Up
            //elements.RemoveAll(e => e.label == Lab.Hatch && e.View == ViewName.Up && upUpHatches.Any(h => h.x1 == e.x1 && h.x2 == e.x2));
            elements.RemoveAll(e => e.label == Lab.Hatch && e.View == ViewName.UpUp && e.y2 < levels.Skip(1).FirstOrDefault());

            // przesuwanie Hatchy, które wychodzą poza obręb UpUp
            var minUpUpX1 = elements.Where(e => e.label == Lab.Up && e.View == ViewName.UpUp).Min(e => e.x1);
            var maxUpUpX2 = elements.Where(e => e.label == Lab.Up && e.View == ViewName.UpUp).Max(e => e.x2);
            foreach (var hatch in elements.Where(e => e.label == Lab.Hatch))
            {
                if (hatch.x1 < minUpUpX1 || hatch.x2 > maxUpUpX2)
                    hatch.View = ViewName.Up;
            }
        }

        private void SelectWallDownChannel(List<Coordinates> elements)
        {
            // wyciągamy downy
            Func<Coordinates, bool> downCondition = e =>
                    (e.label is (Lab.Down_Wall or Lab.Down_DrainTray or Lab.Down_Div or Lab.Middle_Wall)) &&
                    (e.type is (Lab.Wall or Lab.Down_DrainTray or Lab.DrainTray or Lab.Down_Div) ||
                     e.View == ViewName.Down);

            var downWalls = elements.Where(downCondition).ToList();

            if (downWalls.Count == 0)
                return;

            var downUpHatches = elements.Where(a => a.label == Lab.Hatch && a.View == ViewName.DownUp).Select(a => new { a.x1, a.x2 }).ToList();
            var levels = downWalls
                .Select(e => e.y1)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            if (levels.Count > 1)
            {          
                var topLevel = levels[^1]; // najwyższy Y1

                // bierzemy tylko ściany z najwyższego poziomu
                var topLevelWalls = downWalls
                    .Where(e => e.y1.Equals(topLevel))
                    .ToList();

                elements.RemoveAll(e => e.y1 == topLevel && e.View == ViewName.Down && downCondition(e));
                elements.RemoveAll(e => e.y1 != topLevel && e.View == ViewName.DownUp && downCondition(e));
                // usuwanie DownUp
                if (elements.Any(e => e.label == Lab.Middle_Wall))
                {                    
                    var downUpClear = elements
                                  .Where(e => e.y1 == levels[1] &&
                                              e.View == ViewName.DownUp &&
                                              downCondition(e)).ToList();

                elements.RemoveAll(e => !downUpClear.Contains(e) && e.View == ViewName.DownUp);
                }                
                Views.DownUp.Visibility = true;
            }

            // usuwanie duplikatów Hatchy na Up
            //elements.RemoveAll(e => e.label == Lab.Hatch && e.View == ViewName.Down && downUpHatches.Any(h => h.x1 == e.x1 && h.x2 == e.x2));
            elements.RemoveAll(e => e.label == Lab.Hatch && e.View == ViewName.DownUp && e.y2 < levels.Skip(1).FirstOrDefault());

            // przesuwanie Hatchy, które wychodzą poza obręb UpUp
            var minUpUpX1 = elements.Where(e => downCondition(e) && e.View == ViewName.DownUp).Min(e => e.x1);
            var maxUpUpX2 = elements.Where(e => downCondition(e) && e.View == ViewName.DownUp).Max(e => e.x2);
            foreach (var hatch in elements.Where(e => e.label == Lab.Hatch))
            {
                if (hatch.x1 < minUpUpX1 || hatch.x2 > maxUpUpX2)
                    hatch.View = ViewName.Down;
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
            else
            {
                Views.FrameUp.Visibility = false;
            }
        }

        private void SelectRoofUpChannel(List<Coordinates> elements)
        {
            var roofs = elements
                .Where(e => e.label == ViewName.Roof && (e.View == ViewName.Roof))
                .ToList();

            if (roofs.Count == 0)
            {
                Views.Roof.Visibility = false;
                Views.RoofUp.Visibility = false;
                return;
            }

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
            else
            {
                Views.RoofUp.Visibility = false;
            }
        }

        private void MapElementsToViews(List<Coordinates> elements, bool extended)
        {
            void addElement(ViewElement vw, Coordinates el, int y2, string? newLabel)
            {
                var addBlock = new Coordinates
                {
                    View = vw.Name,
                    label = string.IsNullOrEmpty(newLabel) ? el.label : newLabel,
                    type = el.type,
                    x1 = el.x1,
                    x2 = el.x2,
                    y1 = el.y1,
                    y2 = y2 == 0 ? el.y2 : y2,
                    z1 = el.z1,
                    z2 = el.z2,
                    PositionUp = el.PositionUp,
                    PositionDown = el.PositionDown,
                    posUpDown = el.posUpDown,
                    additionalInfos = el.additionalInfos,
                };
                elements.Add(addBlock);
            }

            foreach (var el in elements.ToList())
            {
                if (el.label == "FrontRight")
                    el.label = "RightFront";
                if (el.label == "FrontLeft")
                    el.label = "LeftFront";
            }
            var views = Views.Except("Frame", "FrameUp", "Roof", "Connector");
            foreach (var el in elements.ToList())
            {
                foreach (var vw in views)
                {
                    if (el.label is (Lab.Operational or Lab.Back) &&
                        vw.Name is (ViewName.Up or ViewName.Down or ViewName.UpUp or ViewName.DownUp) &&
                        el.type is (Lab.Wall or Lab.Door or Lab.Removable or Lab.Removable_2 or Lab.Removable_3))
                    {
                        /*
                            Tworzymy elementy pod kwadraciki oznaczających operationale removable / 2 / 3 i back walle na widokach Up, UpUp, Down, DownUp
                
                            Operational: Wall, Door, Removable, Removable_2, Removable_3
                            Back: Wall
                        */
                        var blockIgnore = elements.Any(e => e.label == Lab.Block && e.x1 == el.x1 - 50 && e.x2 == el.x2 + 50);
                        if (!blockIgnore)
                            addElement(vw, el, el.y2 + 50, Lab.Hatch);
                    }

                    if ((el.label == vw.Name || el.label == Lab.Block) && el.type != Lab.Wall)
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label == Lab.Function)
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label is (Lab.AD or Lab.FC or Lab.INTK))
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label is Lab.Porthole && vw.Name is (ViewName.Operational or ViewName.Back))
                    {
                        if (vw.Name == ViewName.Operational && el.z1 < 10)
                        {
                            addElement(vw, el, 0, null);
                        }
                        else if (vw.Name == ViewName.Back && el.z1 > 10)
                        {
                            addElement(vw, el, 0, null);
                        }
                    }

                    if (el.label == Lab.Up && (el.type == Lab.Wall || el.type == Lab.Div) && vw.Name is not (ViewName.Down or ViewName.DownUp))
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label is (Lab.Down_Wall or Lab.Middle_Wall or Lab.Down_Div or Lab.Down_DrainTray) && (el.type is Lab.Wall or Lab.Div or Lab.Down_DrainTray or Lab.DrainTray) && vw.Name is not (ViewName.Up or ViewName.UpUp))
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label is Lab.Back && el.type is Lab.Wall)
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label is Lab.Frame)
                    {
                        addElement(vw, el, 0, null);
                    }

                    if ((el.label is (ViewName.LeftFront or ViewName.RightFront)) && el.type == Lab.Wall && (vw.Name is (ViewName.LeftFront or ViewName.RightFront)))
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label == Lab.SteamGenerator)
                    {
                        addElement(vw, el, 0, null);
                    }
                }
            }

            views = Views.Select("Frame", "FrameUp", "Roof", "RoofUp");
            foreach (var el in elements.ToList())
            {
                foreach (var vw in views)
                {
                    if (el.label is Lab.Frame && vw.Name is (ViewName.Frame or ViewName.FrameUp) && string.IsNullOrEmpty(el.View))
                    {
                        addElement(vw, el, 0, null);
                    }

                    if (el.label is Lab.Roof && vw.Name is (ViewName.Roof or ViewName.RoofUp) && string.IsNullOrEmpty(el.View))
                    {
                        addElement(vw, el, 0, null);
                    }

                    //if (el.label is Lab.Frame && vw.Name is (ViewName.Frame or ViewName.FrameUp))
                    //{
                    //    addElement(vw, el, 0, null);
                    //}

                    //if (el.label is Lab.Roof && vw.Name is (ViewName.Roof or ViewName.RoofUp))
                    //{
                    //    addElement(vw, el, 0, null);
                    //}
                }
            }

            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Operational or Lab.Back) && e.label != ViewName.RightFront);
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Block or Lab.Function));
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && (e.label is Lab.AD or Lab.FC or Lab.INTK));
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label == Lab.Up && (e.type == Lab.Wall || e.type == Lab.Div));
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Down_Wall or Lab.Down_Div or Lab.Down_DrainTray or Lab.Up) && (e.type is Lab.Wall or Lab.Div or Lab.Down_DrainTray or Lab.Down_DrainTray));
            elements.RemoveAll(e => string.IsNullOrWhiteSpace(e.View) && e.label is (Lab.Frame or Lab.Roof));
            elements.RemoveAll(e => e.type is (Lab.Div or Lab.Wall) && e.label != e.View && e.label is not (Lab.Down_Wall or Lab.Middle_Wall or Lab.Down_Div or Lab.Down_DrainTray or Lab.Up) && e.label != Lab.Hatch);
            elements.RemoveAll(e => e.label is (Lab.Frame or Lab.Roof) && e.View is (ViewName.Down or ViewName.DownUp or ViewName.Up or ViewName.UpUp));
            elements.RemoveAll(e => e.label is Lab.Roof && e.View is not (ViewName.Roof or ViewName.RoofUp));
            elements.RemoveAll(e => e.label is Lab.Frame && e.View is (ViewName.Roof or ViewName.RoofUp));
            elements.RemoveAll(e => e.label is (ViewName.Up or Lab.Down_Wall or Lab.Middle_Wall) && e.View is (ViewName.LeftFront or ViewName.RightFront));

            // do weryfikacji
            elements.RemoveAll(e => e.label is (Lab.Frame or Lab.FrameUp) && e.View is (ViewName.Down or ViewName.DownUp or ViewName.Up or ViewName.UpUp));
            elements.RemoveAll(e => e.label is (Lab.Block or Lab.Function) && e.View is (ViewName.Roof or ViewName.RoofUp or ViewName.Frame or ViewName.FrameUp));

            // ikony
            var icons = elements.Where(e => e.label.Contains("icon")).ToList();
            foreach (var icon in icons)
            {
                icon.View = icon.additionalInfos.iconPosition;
            }
        }

        //private void ShowHatchesOnUpDown(List<Coordinates> elements)
        //{
        //    void addElement(ViewElement vw, Coordinates el)
        //    {
        //        var addBlock = new Coordinates
        //        {
        //            View = vw.Name,
        //            label = el.label,
        //            type = el.type,
        //            x1 = el.x1,
        //            x2 = el.x2,
        //            y1 = el.y1,
        //            y2 = el.y2,
        //            z1 = el.z1,
        //            z2 = el.z2,
        //            PositionUp = el.PositionUp,
        //            PositionDown = el.PositionDown,
        //            posUpDown = el.posUpDown,
        //            additionalInfos = el.additionalInfos,
        //        };
        //        elements.Add(addBlock);
        //    }

        //    foreach (var el in elements.ToList())
        //    {
        //        if (el.label == "FrontRight")
        //            el.label = "RightFront";
        //        if (el.label == "FrontLeft")
        //            el.label = "LeftFront";
        //    }
        //    var views = Views.Except("Frame", "Roof", "Connector");
        //    foreach (var el in elements.ToList())
        //    {
        //    }
        //}

        //private void MoveElementsFor_SeparatellyUnits_M(List<Coordinates> elements)
        //{

        //    /*              
        //        EVO-S: Separatelly Units: 
        //        Z odsunięty o 700 
        //        Y odsunięty o 500

        //        Wyszukujemy bloki góra i dół, sprawdzamy czy różnica Z1 wynosi 700 oraz czy różnica pomiędzy y1 wynosi 1000
        //        jeśli tak jest, to w kolejnym kroku szukamy elementów oddalonych maksymalnie o 500 w X,Y,Z od danego toru i tak przyrównujemy co do czego należy 
        //    */
        //    var separatellyUnitsOffset_Z = 700;
        //    var separatellyUnitsOffset_Y = 500;
        //    var maxOffset_Z = 350; // INTK + AD
        //    var maxOffset_Y = 350; // INTK + AD

        //    var blockUp = elements.FirstOrDefault(e => e.label == Lab.Block && e.PositionUp > 0 && e.PositionDown == 0);
        //    var blockDown = elements.FirstOrDefault(e => e.label == Lab.Block && e.PositionDown > 0 && e.PositionUp == 0);
        //    if (blockUp != null && blockDown != null)
        //    {
        //        if (blockUp.z1 - blockDown.z2 == separatellyUnitsOffset_Z && 
        //            blockUp.y1 - blockDown.y2 == separatellyUnitsOffset_Y)
        //        {
        //            var elementsUp = elements.Where(e => (e.z1 <= blockUp.z1 && e.z2 >= blockUp.z2) || (e.z1 >= blockUp.z1 + maxOffset_Z && e.z2 <= blockUp.z2 + maxOffset_Z)).ToList();
        //            var elementsDown = elements.Where(e => (e.z1 <= blockDown.z1 && e.z2 >= blockDown.z2) || (e.z1 >= blockDown.z1 + maxOffset_Z && e.z2 <= blockDown.z2 + maxOffset_Z)).ToList();
        //        }
        //    }            
        //}



        private void MoveElementsFor_SeparatellyUnits_M(List<Coordinates> elements)
        {
            /*              
                EVO-S: Separatelly Units: 
                Z odsunięty o 700 
                Y odsunięty o 500

                Wyszukujemy bloki góra i dół, sprawdzamy czy różnica Z1 wynosi 700 oraz czy różnica pomiędzy y1 wynosi 1000
                jeśli tak jest, to w kolejnym kroku szukamy elementów oddalonych maksymalnie o 500 w X,Y,Z od danego toru i tak przyrównujemy co do czego należy 
            */

            (int min, int max) MinMax(int a, int b) => (Math.Min(a, b), Math.Max(a, b));

            bool BelongsToBlockByCenter(Coordinates e, Coordinates block, int maxOffsetZ, int maxOffsetY)
            {
                var (bzMin, bzMax) = MinMax(block.z1, block.z2);
                var (byMin, byMax) = MinMax(block.y1, block.y2);

                bzMin -= maxOffsetZ; bzMax += maxOffsetZ;
                byMin -= maxOffsetY; byMax += maxOffsetY;

                var (ezMin, ezMax) = MinMax(e.z1, e.z2);
                var (eyMin, eyMax) = MinMax(e.y1, e.y2);

                var ezC = (ezMin + ezMax) / 2.0;
                var eyC = (eyMin + eyMax) / 2.0;

                return ezC >= bzMin && ezC <= bzMax &&
                       eyC >= byMin && eyC <= byMax;
            }

            var separatellyUnitsOffset_Z = 700;
            var separatellyUnitsOffset_Y = 500;
            var maxOffset_Z = 350;
            var maxOffset_Y = 350;

            var blockUp = elements.FirstOrDefault(e => e.label == Lab.Block && e.PositionUp > 0 && e.PositionDown == 0);
            var blockDown = elements.FirstOrDefault(e => e.label == Lab.Block && e.PositionDown > 0 && e.PositionUp == 0);

            if (blockUp == null || blockDown == null) return;

            // (z1 - z2) czy (z2 - z1), to bezpieczniej            
            var dz = Math.Abs((blockUp.z1 - blockDown.z2));
            var dy = Math.Abs((blockUp.y1 - blockDown.y2));

            if (dz == separatellyUnitsOffset_Z && dy == separatellyUnitsOffset_Y)
            {
                var elementsUp = elements
                    .Where(e => !ReferenceEquals(e, blockDown))   // lub e.Id != blockDown.Id
                    .Where(e => BelongsToBlockByCenter(e, blockUp, maxOffset_Z, maxOffset_Y))
                .ToList();

                //bierzemy tę samą referencję w obu blokach: np. "min" narożnik
                var (upZMin, _) = MinMax(blockUp.z1, blockUp.z2);
                var (downZMin, _) = MinMax(blockDown.z1, blockDown.z2);
                var (upYMin, _) = MinMax(blockUp.y1, blockUp.y2);
                var (downYMin, _) = MinMax(blockDown.y1, blockDown.y2);

                var shiftZ = upZMin - downZMin;   // ile "góra" jest przesunięta względem "dołu"
                var shiftY = upYMin - downYMin;

                foreach (var e in elementsUp)
                {
                    e.z1 -= shiftZ;
                    e.z2 -= shiftZ;
                    e.y1 = e.y1 - 350;
                    e.y2 = e.y2 - 350;
                }
                // na frazie nieużywane, ale może się przydać w przyszłości
                //var elementsDown = elements
                //    .Where(e => e.label != Lab.Block)
                //    .Where(e => BelongsToBlock(e, blockDown, maxOffset_Z, maxOffset_Y))
                //    .ToList();
            }
        }

        private bool IsEVO_H(List<Coordinates>? elements)
        {
            if (elements == null || elements.Count == 0)
                return false;

            var blocks = elements
                .Where(e => e != null && e.label == Lab.Block)
                .ToList();

            if (blocks.Count < 2)
                return false;

            var recovery = blocks
                .FirstOrDefault(b => b.PositionUp > 0 && b.PositionDown > 0);

            if (recovery == null)
                return false;

            var normalBlock = blocks
                .FirstOrDefault(b => !ReferenceEquals(b, recovery));

            if (normalBlock == null)
                return false;

            return recovery.z2 > normalBlock.z2 + 200;
        }

        private void Generate2D(List<Coordinates> elements, string fileOutput, bool isExtended, Norm norm)
        {
            dxf2D.isExtended = isExtended;

            var isEvoH = IsEVO_H(elements);
            MoveElementsFor_SeparatellyUnits_M(elements);

            // EVO-S-D: fix na popsute ikony
            //elements.RemoveAll(e => e.z1 == 2101);

            Views.AhuLength = elements.Where(el => el.label == Lab.Block).Max(e => e.x2);
            Views.AhuHeight = elements.Where(el => el.label == Lab.Block).Max(e => e.y2);
            Views.AhuWidth = elements.Where(el => el.label == Lab.Block).Max(e => e.z2);

            var icons = DxfDocument.Load("BLOCKS.dxf");
            var dxf = new DxfDocument();
            var cornerLayer = new Layer("CornerFill") { Color = new AciColor(7) };
            var textLayer = new Layer("Text_Views") { Color = new AciColor(7) };
            var backgroundLayer = new Layer("Background") { Color = new AciColor(7) };

            dxf2D.globalXMin = elements.Min(e => e.x1);
            dxf2D.globalXMax = elements.Max(e => e.x2);
            dxf2D.globalYMin = elements.Min(e => e.y1);
            dxf2D.globalYMax = elements.Max(e => e.y2);
            dxf2D.globalZMin = elements.Min(e => e.z1);
            dxf2D.globalZMax = elements.Max(e => e.z2);

            Views.ApplyNorm(norm);
            Views.SetWaterMark("EVO");

            var cellHeight = Views.AhuHeight > Views.AhuWidth ? Views.AhuHeight + (Views.AhuHeight) : Views.AhuWidth + (Views.AhuWidth);
            var cellWidth = Views.AhuLength + (Views.AhuLength * 1 / 3);
            var grid = new ViewGrid(columns: 5, rows: 10, cellWidth: (int)cellWidth, cellHeight: (int)cellHeight);
            grid.AlignCellToPoint(col: 1, row: 5, worldX: 0, worldY: 0);

            Views.Table.Visibility = false;
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
                var layer = dxf.Layers.Add(new Layer(Lab.Block) { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Block }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp), backgroundLayer);
            }

            void GenerateWalls()
            {
                var layer = dxf.Layers.Add(new Layer("Walls") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Operational, Lab.Up, Lab.Down, Lab.Down_DrainTray, Lab.Down_Wall, Lab.Middle_Wall, Lab.Back, ViewName.LeftFront, ViewName.RightFront }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof), backgroundLayer);
            }

            void GenerateWallsDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Walls_dimension") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Operational, Lab.Up, Lab.Down, Lab.Down_DrainTray, Lab.Down_Wall, Lab.Middle_Wall, Lab.Back }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof), backgroundLayer);
            }

            void DrawBlockDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Block_dimensions") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Block }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp), backgroundLayer);
            }

            void DrawFunctionsWithIcons(bool production)
            {
                var backOffset = Views.Back.XOffset;
                var upOffset = Views.Up.YOffset;
                var upUpOffset = Views.UpUp.YOffset;
                var iconsList = icons.Blocks.ToList();
                var layer = dxf.Layers.Add(new Layer(Lab.Function) { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Function }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp), backgroundLayer);

                // usunięcie ikon Back które powinny zostac zasłonięte
                var iconsBck = elements.Where(e => e.label.Contains("icon") && e.View == ViewName.Back);
                var iconsBckMax = iconsBck.Max(e => e.z2);
                foreach (var iconBack in iconsBck.ToList())
                {
                    if (iconBack.z1 < iconsBckMax)
                        elements.Remove(iconBack);
                }

                // dodawanie ikon
                var sName = string.Empty;
                var distinctList = elements.DistinctBy(e => (e.posUpDown, e.x1, e.y1, e.z1)).Where(i => i.label.Contains("icon") && i.additionalInfos != null).ToList();

                foreach (var b in icons.Blocks)
                {
                    // jeśli bloki ikon są zawsze 100x100 i rysowane od (0,0) do (100,100)
                    b.Origin = new Vector3(50, 50, 0);
                }

                foreach (var icon in distinctList)
                {
                    sName = icon.additionalInfos.iconName;
                    bool isExhaust = icon.additionalInfos.airPath.ToLower() == "exhaust";
                    if (Dxf2DService.IconMap.ContainsKey(sName!))
                    {
                        var insertIcon = iconsList.FirstOrDefault(b => b.Name.Equals(sName, StringComparison.OrdinalIgnoreCase));
                        // dxf rotuje przeciwnie do ruchu wskazówek zegara
                        static double Normalize360(double deg)
                        {
                            deg %= 360.0;
                            if (deg < 0) deg += 360.0;
                            return deg;
                        }

                        var cw = icon.additionalInfos.iconRotation;
                        var dxfIconRotation = Normalize360(360.0 - cw);
                        switch (icon.additionalInfos.iconPosition)
                        {
                            case ViewName.Operational:
                                var insertIconOperational = new Insert(insertIcon)
                                {
                                    Position = new Vector3(icon.x1 + 50, icon.y1 + 50, 0),
                                    Layer = layer,
                                    Scale = new Vector3(1, 1, 1),
                                    Rotation = dxfIconRotation
                                };

                                if (icon.View == ViewName.Operational)
                                    dxf.Entities.Add(insertIconOperational);

                                break;

                            case ViewName.Back:
                                {
                                    var cx = icon.x1 + 50.0;
                                    var cy = icon.y1 + 50.0;

                                    var cxBack = (dxf2D.globalXMax + dxf2D.globalXMin) - cx;

                                    var insertIconBack = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(cxBack + backOffset, cy, 0),
                                        Layer = layer,
                                        Scale = new Vector3(1, 1, 1),
                                        Rotation = dxfIconRotation
                                    };

                                    if (icon.View == ViewName.Back)
                                        dxf.Entities.Add(insertIconBack);

                                    break;
                                }

                            case ViewName.Up:
                                {
                                    var cx = icon.x1 + 50.0;
                                    var cz = icon.z1 + 50.0;

                                    var insertIconUp = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(cx, cz + upOffset, 0),
                                        Layer = layer,
                                        Scale = new Vector3(1, 1, 1),
                                        Rotation = dxfIconRotation
                                    };

                                    dxf.Entities.Add(insertIconUp);
                                    break;
                                }

                            case ViewName.UpUp when production:
                                {
                                    var cx = icon.x1 + 50.0;
                                    var cz = icon.z1 + 50.0;

                                    var insertIconUpUp = new Insert(insertIcon)
                                    {
                                        Position = new Vector3(cx, cz + upUpOffset, 0),
                                        Layer = layer,
                                        Scale = new Vector3(1, 1, 1),
                                        Rotation = dxfIconRotation
                                    };

                                    dxf.Entities.Add(insertIconUpUp);
                                    break;
                                }

                            default:
                                break;
                        }
                    }
                }
            }

            void DrawFunctionsDimensions()
            {
                var layer = dxf.Layers.Add(new Layer("Function_dimensions") { Color = new AciColor(7), IsVisible = false });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Function }, true, false, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp), backgroundLayer);
            }

            void DrawExternalElements()
            {
                var layer = dxf.Layers.Add(new Layer("ExternalElements") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Hole, Lab.AD, Lab.FC, Lab.INTK, Lab.Connector }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp), backgroundLayer);

                var layerDim = dxf.Layers.Add(new Layer("ExternalElements_dimensions") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Hole, Lab.AD, Lab.FC, Lab.INTK, Lab.Connector, Lab.InsideConnector }, true, false, layerDim, textLayer, Views.Except(ViewName.Frame, ViewName.FrameUp, ViewName.Roof, ViewName.RoofUp), backgroundLayer);
            }

            void GeneratePorthole()
            {
                var layer = dxf.Layers.Add(new Layer("Porthole") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Porthole }, false, true, layer, textLayer, Views.Except(ViewName.Frame, ViewName.Roof), backgroundLayer);
            }

            void GenerateRips()
            {
                var layer = dxf.Layers.Add(new Layer("Rips") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Hatch }, false, true, layer, textLayer, Views.Select(ViewName.Up, ViewName.UpUp, ViewName.Down, ViewName.DownUp), backgroundLayer);
            }

            void GeneratePortholeDimension()
            {
                var layerDim = dxf.Layers.Add(new Layer("Porthole_dimensions") { Color = new AciColor(7), IsVisible = false });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Porthole }, true, false, layerDim, textLayer, Views.Except(ViewName.Frame, ViewName.Roof), backgroundLayer);
            }

            void GenerateSwitchbox()
            {
                var layer = dxf.Layers.Add(new Layer("Switchbox") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Switchbox }, false, true, layer, textLayer, Views.Select(ViewName.Operational, ViewName.Back), backgroundLayer);
            }

            void GenerateSwitchboxDimension()
            {
                var layerDim = dxf.Layers.Add(new Layer("Switchbox_dimensions") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Switchbox }, true, false, layerDim, textLayer, Views.Select(ViewName.Operational, ViewName.Back), backgroundLayer);
            }

            void GenerateFrame()
            {
                var layerFrame = dxf.Layers.Add(new Layer("Frame") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Frame, Lab.FrameUp }, false, true, layerFrame, textLayer, Views.Except(ViewName.Up, ViewName.Down, ViewName.Roof), backgroundLayer);
            }

            void GenerateFrameDimensions()
            {
                elements = elements.OrderByDescending(e => e.x2).ThenBy(e => e.z2).ToList();
                var layerFrameDim = dxf.Layers.Add(new Layer("Frame_dimensions") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Frame }, true, false, layerFrameDim, textLayer, Views.Except(ViewName.Up, ViewName.Down, ViewName.Roof), backgroundLayer);
            }

            void GenerateRoof()
            {
                var layerRoof = dxf.Layers.Add(new Layer("Roof") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Roof }, false, true, layerRoof, textLayer, Views.Select(ViewName.Roof, ViewName.RoofUp), backgroundLayer);
            }

            void GenerateRoofDimensions()
            {
                var layerRoofDim = dxf.Layers.Add(new Layer("Roof_dimensions") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.Roof }, true, false, layerRoofDim, textLayer, Views.Select(ViewName.Roof, ViewName.RoofUp), backgroundLayer);
            }

            void GenerateSteamGenerator(string[] exceptViews)
            {
                var layerRoof = dxf.Layers.Add(new Layer("SteamGenerator") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.SteamGenerator }, false, true, layerRoof, textLayer, Views.Except(exceptViews), backgroundLayer);
            }

            void GenerateSteamGeneratorDimensions()
            {
                var layerRoofDim = dxf.Layers.Add(new Layer("SteamGenerator_dimensions") { Color = new AciColor(7) });
                dxf2D.GenerateView(dxf, elements, new List<string> { Lab.SteamGenerator }, true, false, layerRoofDim, textLayer, Views.Select(ViewName.Roof, ViewName.RoofUp), backgroundLayer);
            }

            MapElementsToViews(elements, isExtended);
            PrepareElementsToMode(elements, isExtended);
            //IconRotation_CorrectXY(elements);
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
            SetElementsVisibility(elements, [Lab.Frame, Lab.FrameUp], Views.Except(ViewName.Frame), null, false);
            SetElementsVisibility(elements, [Lab.Frame], Views.Select(ViewName.RightFront), true, true);
            //HideDimensions(elements);
            // widoczność wymiarów blokow
            //SetElementsVisibility(elements, [Lab.Block], Views.Select(ViewName.Operational), true, true);
            Connector_AddInsideCircle(elements);
            RepositioningOnGridWhenViewsHide(norm, grid);

            // jeśli jest generator pary na froncie, to rysujemy go najpierw na operational, Bloki, potem SteamGen na back
            var steamGenOnFront = elements.Any(e => e.type == "SteamGenerator_Front");
            if (steamGenOnFront)
            {
                GenerateSteamGenerator([ViewName.Frame, ViewName.Roof, ViewName.Operational]);
                DrawBlocks();
                DrawBlockDimensions();
                GenerateSteamGenerator([ViewName.Frame, ViewName.Roof, ViewName.Back]);
                GenerateSteamGeneratorDimensions();
            }
            else
            {
                GenerateSteamGenerator([ViewName.Frame, ViewName.Roof, ViewName.Back]);
                DrawBlocks();
                DrawBlockDimensions();
                GenerateSteamGenerator([ViewName.Frame, ViewName.Roof, ViewName.Operational]);
                GenerateSteamGeneratorDimensions();
            }

            if (isExtended)
            {
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
                //GeneratePortholeDimension();
                GenerateRips();
                GenerateSwitchbox();
                GenerateSwitchboxDimension();
            }
            else
            {
                DrawFunctionsWithIcons(isExtended);
                //DrawFunctionsDimensions();
                DrawExternalElements();
                //GenerateFrame();
                //GenerateFrameDimensions();
                //GenerateRoof();
                //GenerateRoofDimensions();
                GeneratePorthole();
                //GeneratePortholeDimension();
                //GenerateRips();
                GenerateSwitchbox();
                //GenerateSwitchboxDimension();
            }            

            //ArrowService.AddArrow(
            //    dxf,
            //    anchor: new Vector2(0, 1970),
            //    direction: ArrowDirection.Right,
            //    label: "ETA",
            //    arrowSize: 120,
            //    padding: 20,
            //    outlineColor: AciColor.Red,
            //    layer: arrowLayer,
            //    filled: false,
            //    textStyle: style
            //);

            // budowanie listy dla znaczników płyt, aby walle Operational i Back były widoczne na Up i Down
            //ShowHatchesOnUpDown(elements);

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
            if (!isExtended)
            {
                dxf2D.AddBigOneWatermark(dxf, textLayer, elements, Views.Operational, "EVO", isEvoH);
            }

            //DrawTable(dxf);

            // usuwanie duplikatów wymiarów
            DedupLinearDimensions(dxf);
            dxf.Save(fileOutput);
            foreach (var vw in Views.All)
            {
                var cell = GridPresets.Cells[norm][vw.Name];
                var cellY = grid.GetCellOrigin(cell.col, cell.row).y;

                Debug.WriteLine($"{vw.Name} | cellY={cellY} | viewYOffset={vw.YOffset}");
            }
        }

        void RemoveDuplicates(List<Coordinates> elements)
        {
            var set = new HashSet<(int x1, int x2, int y1, int y2, int z1, int z2,
                                   string label, string view)>();

            elements.RemoveAll(e =>
            {
                var key = (e.x1, e.x2,
                           e.y1, e.y2,
                           e.z1, e.z2,
                           e.label, e.View);

                return !set.Add(key); // jeśli już istnieje → usuń
            });
        }

        void Connector_AddInsideCircle(List<Coordinates> elements)
        {
            var connectors = elements.Where(e => e.label == Lab.Connector).ToList();
            foreach (var connector in connectors.ToList())
            {
                var circle = new Coordinates
                {
                    label = "Connector",
                    type = Lab.InsideConnector,
                    x1 = connector.x1 + 3,
                    x2 = connector.x2 - 3,
                    y1 = connector.y1,
                    y2 = connector.y2,
                    z1 = connector.z1,
                    z2 = connector.z2,
                    View = connector.View,
                    Show = connector.Show,
                    ShowDimension = connector.ShowDimension,
                };
                elements.Add(circle);
                connector.Show = false;
                connector.ShowDimension = false;
            }
        }

        void HideDimensions(List<Coordinates> elements)
        {
            bool IsFrameDim(Coordinates e) =>
                (e.label is Lab.Frame or Lab.FrameUp) &&
                (e.View is ViewName.Frame or ViewName.FrameUp);

            var frameDims = elements.Where(IsFrameDim);

            var groups = frameDims.GroupBy(e =>
            {
                var dx = Math.Abs(e.x2 - e.x1);
                var dy = Math.Abs(e.y2 - e.y1);
                var dz = Math.Abs(e.z2 - e.z1);

                return (e.View, e.label, dx, dy, dz);
            });

            foreach (var g in groups)
            {
                bool first = true;
                foreach (var el in g)
                {
                    if (first)
                    {
                        // el.ShowDimension = true;
                        first = false;
                    }
                    else
                    {
                        el.ShowDimension = false;
                    }
                }
            }
        }


        static double R(double v, double step = 0.01) => Math.Round(v / step) * step;

        static string Key(LinearDimension d)
        {
            var a = d.FirstReferencePoint;
            var b = d.SecondReferencePoint;

            string p1 = $"{R(a.X)},{R(a.Y)}";
            string p2 = $"{R(b.X)},{R(b.Y)}";
            var pts = string.CompareOrdinal(p1, p2) <= 0 ? $"{p1}|{p2}" : $"{p2}|{p1}";

            return $"{pts}__rot:{R(d.Rotation, 0.001)}__off:{R(d.Offset)}__layer:{d.Layer?.Name}";
        }

        static void DedupLinearDimensions(DxfDocument dxf)
        {
            // Wariant A: tylko wymiary (najczyściej)
            var dims = dxf.Entities.Dimensions.OfType<LinearDimension>().ToList();

            // Wariant B: jeśli w wersji nie ma Dimensions, to:
            // var dims = dxf.Entities.All.OfType<LinearDimension>().ToList();

            var keep = dims.DistinctBy(Key).ToHashSet();

            foreach (var dim in dims)
                if (!keep.Contains(dim))
                    dxf.Entities.Remove(dim);
        }

        void DrawTable(DxfDocument dxf)
        {
            var data = new List<(string Element, string Quantity, string Date)>
            {
                ("AD",        "2", "11.2025"),
                ("FC",        "2", "-"),
                ("INTK",      "0", "-"),
                ("DrainTray", "3", "03.2024"),
                ("Portholes", "2", "09.2023")
            };

            CreateTable(dxf, offset_x: Views.Table.XOffset, offset_y: Views.Table.YOffset, rows: data, title: "NW1 EVO-S Compact");
        }

        private void CreateTable(
            DxfDocument dxf,
            double offset_x,
            double offset_y,
            IEnumerable<(string Element, string Quantity, string Date)> rows,
            string title = "NW1 EVO-S Compact",
            double cellWidth = 300,
            double cellHeight = 100)
        {
            // --- helpers ---
            void AddText(string text, double x, double y, double height, bool bold = false)
            {
                var t = new Text(text, new Vector3(x, y, 0), height)
                {
                    Alignment = TextAlignment.MiddleCenter
                };
                if (bold) t.Style = LabelTextStyles.ArialBold;
                dxf.Entities.Add(t);
            }

            void AddLine(double x1, double y1, double x2, double y2, Lineweight lw = Lineweight.ByLayer)
            {
                var line = new netDxf.Entities.Line(new Vector3(x1, y1, 0), new Vector3(x2, y2, 0)) { Lineweight = lw };
                dxf.Entities.Add(line);
            }

            // --- geometry ---
            double startX = offset_x;
            double startY = offset_y;

            int cols = 3;
            int dataCount = rows is ICollection<(string, string, string)> c ? c.Count : rows.Count(); // policz wiersze
            int gridRows = 1 + dataCount; // 1 = wiersz nagłówka; tytuł jest NAD tabelą

            // siatka pozioma
            for (int i = 0; i <= gridRows; i++)
            {
                double y = startY - i * cellHeight;
                var lw = (i == 0 || i == gridRows) ? Lineweight.W50 : Lineweight.W25;
                AddLine(startX, y, startX + cols * cellWidth, y, lw);
            }
            // siatka pionowa
            for (int j = 0; j <= cols; j++)
            {
                double x = startX + j * cellWidth;
                var lw = (j == 0 || j == cols) ? Lineweight.W50 : Lineweight.W25;
                AddLine(x, startY, x, startY - gridRows * cellHeight, lw);
            }

            // środki kolumn
            double cx0 = startX + 0.5 * cellWidth;
            double cx1 = startX + 1.5 * cellWidth;
            double cx2 = startX + 2.5 * cellWidth;

            // rozmiary czcionek (skalują się z komórką)
            double titleTextH = cellHeight * 0.55;
            double headerTextH = cellHeight * 0.35;
            double dataTextH = cellHeight * 0.32;

            // tytuł nad tabelą
            double centerX = startX + (cols * cellWidth) / 2.0;
            double titleGap = cellHeight * 0.60;
            double titleY = startY + titleGap;
            AddText(title, centerX, titleY, titleTextH, bold: true);

            // nagłówki (wiersz 1 siatki)
            double headerY = startY - 0.5 * cellHeight;
            AddText("Element", cx0, headerY, headerTextH);
            AddText("Quantity", cx1, headerY, headerTextH);
            AddText("Date", cx2, headerY, headerTextH);

            // dane
            int iRow = 0;
            foreach (var (Element, Quantity, Date) in rows)
            {
                double cy = startY - ((iRow + 1) + 0.5) * cellHeight; // +1 bo po nagłówku
                AddText(Element ?? "", cx0, cy, dataTextH);
                AddText(Quantity ?? "", cx1, cy, dataTextH);
                AddText(Date ?? "", cx2, cy, dataTextH);
                iRow++;
            }
        }


        private void RepositioningOnGridWhenViewsHide(Norm norm, ViewGrid grid)
        {
            switch (norm)
            {
                case Norm.ISO_EXTENDED:
                    // jeśli UpUp jest niewidoczny, to RoofUp też będzie niewidoczny
                    if (Views.UpUp.Visibility == false)
                    {
                        if (GridPresets.Cells.TryGetValue(Norm.ISO_EXTENDED, out var views))
                        {
                            views[ViewName.Up] = (1, 4);
                            views[ViewName.Roof] = (1, 3);
                        }
                    }
                    if (Views.DownUp.Visibility == false)
                    {
                        if (GridPresets.Cells.TryGetValue(Norm.ISO_EXTENDED, out var views))
                        {
                            views[ViewName.Frame] = (1, 7);
                        }
                    }
                    break;

                case Norm.US_EXTENDED:
                    // jeśli UpUp jest niewidoczny, to RoofUp też będzie niewidoczny
                    if (Views.UpUp.Visibility == false)
                    {
                        if (GridPresets.Cells.TryGetValue(Norm.US_EXTENDED, out var views))
                        {
                            views[ViewName.Down] = (1, 4);
                            views[ViewName.Roof] = (1, 7);
                            views[ViewName.Frame] = (1, 3);
                        }
                    }
                    break;

                case Norm.PROD_EXTENDED:

                    break;
                default:
                    break;
            }
            // update
            Views.ApplyNormOnGrid(norm, grid);
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
