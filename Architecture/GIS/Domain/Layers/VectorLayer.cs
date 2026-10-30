
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Architecture.GIS.Domain.Models;

namespace Architecture.GIS.Domain.Layers
{
    public class VectorLayer
    {
        public string Name { get; set; }
        public bool Visible { get; set; } = true;
        public List<MapMarker> Markers { get; } = new List<MapMarker>();
    }
}
