using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Architecture.GIS.Abstractions
{
    // ==========================================
    // 2. Tile Service & Disk/Memory Caching
    // ==========================================
    interface ITileProvider
    {
        Task<Image> GetTileAsync(int x, int y, int zoom);
    }
}
/*
 * interface.
 * Architecture Level: Abstractions/Contracts
 * Role and Scope of Responsibility: A contract interface. It defines the rules for retrieving map tiles.
Located at: GIS/Abstractions/ITileProvider.cs.

Why it is placed here: It is independent of any specific implementation (whether it be OpenStreetMap, Yandex, or a local cache). It defines only the `GetTileAsync(...)` method.
*/
