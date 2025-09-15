using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Consts
{
    public static class ViewName
    {
        public const string Operational = "Operational";
        public const string Back = "Back";
        public const string Up = "Up";
        public const string Down = "Down";
        public const string LeftFront = "LeftFront";
        public const string RightFront = "RightFront";
    }

    public class ViewElement
    {
        public string Name { get; set; }

        public int XOffset { get; set; }

        public int YOffset { get; set; }

        public ViewElement(string name, int x, int y) =>
            (Name, XOffset, YOffset) = (name, x, y);
    }

    public class ViewsList
    {
        private Dictionary<string, ViewElement> _views = new()
        {
            [ViewName.Operational] = new(ViewName.Operational, 0, 0),
            [ViewName.Back] = new(ViewName.Back, 0, 5000),
            [ViewName.Up] = new(ViewName.Up, 0, 10000),
            [ViewName.Down] = new(ViewName.Down, 0, 15000),
            [ViewName.LeftFront] = new(ViewName.LeftFront, 0, 20000),
            [ViewName.RightFront] = new(ViewName.RightFront, 0, 25000)
        };

        public ViewElement this[string name] => _views[name];

        public ViewElement Operational => _views[ViewName.Operational];
        public ViewElement Back => _views[ViewName.Back];
        public ViewElement Up => _views[ViewName.Up];
        public ViewElement Down => _views[ViewName.Down];
        public ViewElement LeftFront => _views[ViewName.LeftFront];
        public ViewElement RightFront => _views[ViewName.RightFront];

        public IEnumerable<ViewElement> All => _views.Values;
    }


}
