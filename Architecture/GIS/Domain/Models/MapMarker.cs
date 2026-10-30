using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Architecture.GIS.Domain.Models
{
    // ==========================================
    // 3. Domain Model (Retail & Food Stores)
    // ==========================================
    public enum StoreType { Retail, Grocery, Supermarket }

    public class MapMarker
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public GeoCoordinate Location { get; set; }
        public StoreType Type { get; set; }
        public System.Drawing.Color Color { get; set; }

        public MapMarker(string name, GeoCoordinate location, StoreType type, Color color)
        {
            Name = name;
            Location = location;
            Type = type;
            Color = color;
        }

        public virtual void Render(Graphics g, PointF screenPos)
        {
            int size = 16;
            var rect = new RectangleF(screenPos.X - size / 2f, screenPos.Y - size / 2f, size, size);

            using (var brush = new SolidBrush(Color))
            using (var pen = new Pen(Color.White, 2))
            {
                g.FillEllipse(brush, rect);
                g.DrawEllipse(pen, rect);
            }

            using (var font = new Font("Segoe UI", 8, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.Black))
            {
                g.DrawString(Name, font, textBrush, screenPos.X + 10, screenPos.Y - 6);
            }
        }
    }
}
