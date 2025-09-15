using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klimor.WebApi.DXF.Consts
{
    public static class Lab
    {
        // blocks & functions
        public const string Block = "Block";
        public const string Function = "Function";

        // walls
        public const string Wall = "Wall";
        public const string Up = "Up";
        public const string Down = "Down";
        public const string Down_DrainTray = "Down_DrainTray";
        public const string Down_Wall = "Down_Wall";
        public const string Back = "Back";
        public const string Operational = "Operational";
        public const string Door = "Door";
        public const string Removable = "Removable";        // PNL_GRIP
        public const string Removable_2 = "Removable_2";    // PNL_HH
        public const string Removable_3 = "Removable_3";    // PNL_BSH

        // components
        public const string Roof = "Roof";
        public const string Frame = "Frame";        

        // external elements
        public const string Hole = "Hole";
        public const string AD = "AD";
        public const string FC = "FC";
        public const string INTK = "IO";

        // drawing
        public const string Icon = "Icon";

        public static string[] ExternalElements = new string[] { Hole, AD, FC, INTK };        
    }
}
