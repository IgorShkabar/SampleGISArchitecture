using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Architecture.GIS.Abstractions
{
    public interface ISpatialIndex<T>
    {
        void Insert(Envelope envelope, T item);
        void Insert(T item, float x, float y);
        void Build();
        IList<T> Query(Envelope searchEnv);
        void Clean();
        // Returns only the points that fall within the screen's bounding box
        IEnumerable<T> Search(double minX, double minY, double maxX, double maxY);
        int Search(Envelope currentViewport);
    }
}
