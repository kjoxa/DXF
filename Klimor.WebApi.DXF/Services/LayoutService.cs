using netDxf;
using netDxf.Entities;
using netDxf.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Services
{
    public class LayoutService
    {
        // przykładowe generowanie layoutów
        public void GenerateLayouts(DxfDocument dxf)
        {
            // Dodajemy przykładową geometrię do ModelSpace
            var line = new netDxf.Entities.Line(new Vector2(0, 0), new Vector2(100, 100));
            dxf.Entities.Add(line);

            for (int i = 1; i <= 10; i++)
            {
                string layoutName = "Layout" + i;

                // Domyślny rozmiar arkusza (np. A4) – jednostki w calach
                var layout = new Layout(layoutName);
                // jednostki papieru
                layout.PlotSettings.PaperUnits = PlotPaperUnits.Milimeters;

                // rozmiar A4 w mm (portret)
                layout.PlotSettings.PaperSize = new netDxf.Vector2(210, 297);

                // (opcjonalnie) nazwa formatu – zwykły string
                layout.PlotSettings.PaperSizeName = "ISO A4 210 x 297 mm";

                // (opcjonalnie) obrócenie strony na poziomą
                layout.PlotSettings.PaperRotation = PlotRotation.Degrees90;

                dxf.Layouts.Add(layout);
                // rysunek ramki arkusza
                var rect = new Polyline2D(new[]
                {
                new Vector2(0,0),
                new Vector2(210,0),
                new Vector2(210,297),
                new Vector2(0,297)
                }, true); 
                // zamknięty
                layout.AssociatedBlock.Entities.Add(rect);

                // Dodajemy layout do dokumentu
                dxf.Layouts.Add(layout);

                // nie wiem po co, ale jest
                var vp = new Viewport(
                    new netDxf.Vector2(105, 148.5),    // środek w "papierze"
                    new netDxf.Vector2(180, 240)       // rozmiar viewportu
                )
                {
                    // Ustawienia widoku modelu wewnątrz viewportu
                    ViewCenter = new netDxf.Vector2(50, 50), // co ma być w centrum
                    ViewHeight = 300                         // „zoom” (większa wartość = mniejsze powiększenie)
                };

                layout.AssociatedBlock.Entities.Add(vp);
            }

            // Zapis do pliku DXF
            dxf.Save("test_layouts.dxf");
        }
    }
}
