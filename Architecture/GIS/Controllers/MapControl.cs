using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Architecture.GIS.DTOs;
using Architecture.GIS.Abstractions;
using Architecture.GIS.StreamingRTree;
using System.Threading;
using NetTopologySuite.Geometries;
using Architecture.GIS.UI;

namespace Architecture.GIS.Controllers
{
    public partial class MapControl<T> where T : class
    {
        // Инжектируется или инициализируется при загрузке карты
        private readonly ISpatialIndex<MapPointDto> _spatialIndex = new SpatialIndexManager<MapPointDto>();
        private CancellationTokenSource _cts;
        private SimdAABB viewport;
        private readonly ITileService _tileService;


        // Главное правило: событие UI вызывает асинхронный пайплайн
        private async void OnMapViewportChanged(Envelope currentViewport)
        {
            // 1. Отменяем предыдущую фоновую загрузку тайлов, если карта еще двигается
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // Небольшой дебаунс (например, 50мс), чтобы не перегружать процессор при плавной анимации скролла
                await Task.Delay(50, token);

                // 2. ШАГ 1: Быстрая фильтрация точек в памяти (Занимает < 1-2 мс для R-Tree)
                // Выполняем в Task.Run, чтобы полностью освободить GUI от расчетов
                List<MapPointDto> pointsInView = await Task.Run(() =>
                    new List<MapPointDto>(_spatialIndex.Search(currentViewport)), token);

                // 3. ШАГ 2: Асинхронное скачивание тайлов/доп. данных с мап-сервера в фоне
                // Передаем токен отмены, чтобы прекратить скачивание, если юзер ушел с этого Viewport
                var renderData = await FetchMapTilesAndDetailsAsync(currentViewport, pointsInView, token);

                // 4. ШАГ 3: Отрисовка. Так как мы использовали await, мы автоматически 
                // вернулись в GUI-поток (SynchronizationContext).
                //RenderMarkersOnMap(renderData);
                await RefreshViewportAsync(viewport);
            }
            catch (OperationCanceledException)
            {
                // Запрос был отменен, так как Viewport снова изменился. Игнорируем.
            }
            catch (Exception ex)
            {
                // Логирование ошибок сети/рендеринга
                //Logger.LogError(ex);
            }
        }

        // Фоновый метод: Сеть и тяжелая сборка данных
        private async Task<RenderPackage> FetchMapTilesAndDetailsAsync(
    Envelope viewport,
    List<MapPointDto> points,
    CancellationToken token)
        {
            // Принудительно уходим в фоновый поток (ThreadPool)
            return await Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();

                // 1. Convert the Envelope to SimdAABB
                // Note: Update the property names (like MinX, MaxX) to match your SimdAABB definition
                var simdViewport = new SimdAABB(
                    (float)viewport.MinX,
                    (float)viewport.MinY,
                    (float)viewport.MaxX,
                    (float)viewport.MaxY
                );

                // 2. Pass the converted object into the method
                // Имитация или реальный вызов HttpClient для загрузки подложки (тайлов)
                var tiles = await Task.Run(() => LoadTilesForViewport(simdViewport));
// Формируем пакет данных, готовый к моментальной отрисовке
                return new RenderPackage(tiles, points);
            }, token);
        }

        private readonly MapCanvas _mapCanvas = new MapCanvas();


        // Метод обновления экрана (вызывается при Pan / Zoom / Resize)
        public async Task RefreshViewportAsync(SimdAABB viewport)
        {
            try
            {
                // 1. Асинхронно загружаем тайлы в фоновом потоке, чтобы GUI не фризился
                var tiles = await Task.Run(() => LoadTilesForViewport(viewport));

                // 2. Асинхронно фильтруем 100 000+ точек через R-Tree в фоновом потоке
                // Графический движок получит только ~100-1000 точек, видимых на экране
                var visibleMarkers = await Task.Run(() =>
                    _spatialIndex.Search(viewport.MinX, viewport.MinY, viewport.MaxX, viewport.MaxY)
                );

                // ИСПРАВЛЕНИЕ CS0815: Отрисовка на GUI должна происходить в основном потоке.
                // Вызываем методы у конкретного объекта (_mapCanvas), а не у 'object'
                _mapCanvas.Clear();                             // Строка 84 больше не выдает ошибку
                _mapCanvas.DrawTiles(tiles);                    // Строка 85 исправлена
                _mapCanvas.DrawMarkers(visibleMarkers);         // Строка 86 исправлена
            }
            catch (Exception ex)
            {
                // Логирование ошибок отрисовки
                System.Diagnostics.Debug.WriteLine($"Ошибка рендеринга: {ex.Message}");
            }
        }


        private List<Tile> LoadTilesForViewport(SimdAABB simd_axis_aligned_bounding_box)
        {
            // Имитация асинхронного скачивания/выгрузки тайлов с maps-сервера
            return new List<Tile>();
        }

    }
}
