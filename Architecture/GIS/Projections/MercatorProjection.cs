using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Architecture.GIS.Projections
{
    public class MercatorProjection
    {
        public static PointF LatLonToPixel(GIS.Domain.Models.GeoCoordinate coord, int zoom, int tileSize = 256)
        {
            double mapSize = tileSize * (1 << zoom);
            double x = (coord.Longitude + 180.0) / 360.0;
            double sinLat = Math.Sin(coord.Latitude * Math.PI / 180.0);
            double y = 0.5 - Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * Math.PI);

            return new PointF((float)(x * mapSize), (float)(y * mapSize));
        }

        public static GIS.Domain.Models.GeoCoordinate PixelToLatLon(PointF pixel, int zoom, int tileSize = 256)
        {
            double mapSize = tileSize * (1 << zoom);
            double x = pixel.X / mapSize - 0.5;
            double y = 0.5 - pixel.Y / mapSize;

            double lon = 360.0 * x;
            double lat = 90.0 - 360.0 * Math.Atan(Math.Exp(-y * 2 * Math.PI)) / Math.PI;

            return new GIS.Domain.Models.GeoCoordinate(lat, lon);
        }
    }
}
/* 
 * Уровень Архитектуры: Core / Math
 * Роль и Зона ответственности: вычислительный сервис. Преобразует гео-координаты в пиксели ($EPSG:3857$). */
