using System.Threading;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;

namespace Architecture.GIS.Controllers
{
    internal interface ITileService
    {        
        Task DownloadTilesForViewportAsync(Envelope viewport, CancellationToken token);
    }
}