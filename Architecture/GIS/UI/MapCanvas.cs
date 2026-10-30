using Architecture.GIS.Domain.Models;
using Architecture.GIS.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Architecture.GIS.UI
{
    internal class MapCanvas
    {
        public void Clear() { }
        public void DrawTiles(IEnumerable<Tile> tiles) { }
        public void DrawMarkers(IEnumerable<MapPointDto> markers) { }
    }
}
