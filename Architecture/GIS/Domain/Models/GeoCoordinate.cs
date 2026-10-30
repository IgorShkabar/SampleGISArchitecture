using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace Architecture.GIS.Domain.Models
{
    // ==========================================
    // 1. Core Geo & Math (Web Mercator EPSG:3857)
    // ==========================================

    public struct GeoCoordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public GeoCoordinate(double lat, double lon)
        {
            Latitude = lat;
            Longitude = lon;
        }
    }
}
/* Структура.
 * Уровень Архитектуры: Domain (Model) 
 Базовая сущность данных. Хранит широту/долготу ($\text{Lat/Lon}$). 
 */
