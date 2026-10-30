using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Architecture.GIS.DTOs
{
    internal class RenderPackage
    {
        public List<Tile> Tiles { get; }
        public List<MapPointDto> PointsInView { get; }
        public RenderPackage(List<Tile> tiles, List<MapPointDto> points) { Tiles = tiles; PointsInView = points; }
    }
}
