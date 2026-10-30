using Architecture.GIS.StreamingRTree;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Architecture.GIS.Controllers
{
    public class MapViewController<T> where T : class
    {
        private readonly SpatialIndexManager<T> _spatialIndex;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        // Define this once at the class level
        private HttpClient _httpClient = new HttpClient();


        internal MapViewController(SpatialIndexManager<T> spatialIndex)
        {
            _spatialIndex = spatialIndex;
        }

        // Вызывается из GUI-потока при изменении границ экрана (OnViewportChanged)
        public async void OnViewportChanged(Envelope currentViewport)
        {
            // 1. Отменяем предыдущую задачу поиска и загрузки тайлов
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // 2. Выносим тяжелый пространственный поиск в фоновый поток (ThreadPool)
                IList<T> visiblePoints = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    return _spatialIndex.Query(currentViewport);
                }, token);

                // 3. Асинхронно запускаем параллельную загрузку необходимых тайлов/данных
                await FetchMapTilesAsync(currentViewport, token);

                // 4. Возвращаемся в GUI поток и отдаем отфильтрованные точки на отрисовку
                RenderPointsOnGui(visiblePoints);
            }
            catch (OperationCanceledException)
            {
                // Запрос был отменен новым сдвигом карты — игнорируем ошибку
            }
            catch (Exception ex)
            {
                // Логирование критических ошибок
                Console.WriteLine($"Ошибка рендеринга: {ex.Message}");
            }
        }

        // Асинхронная выгрузка тайлов без блокировки UI
        private async Task FetchMapTilesAsync(Envelope viewport, CancellationToken token)
        {
            List<Uri> tileUrls = CalculateRequiredTiles(viewport);

            var downloadTasks = new List<Task>();

            // НЕ ВЫЗЫВАЙ ЭТОТ КОД: юзай файл MapTileDownloader
            using (_httpClient = new System.Net.Http.HttpClient())
            {
                foreach (var url in tileUrls)
                {
                    token.ThrowIfCancellationRequested();

                    // Скачиваем каждый тайл асинхронно во внутренний буфер/диск
                    downloadTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
#if NET8_0_OR_GREATER
                            byte[] tileData = await httpClient.GetByteArrayAsync(url, token).ConfigureAwait(false);
                            // Сохранение в кш / оперативное декодирование изображения тайла...
#endif                     
                        }
                        catch (Exception) { /* Обработка ошибок сети для конкретного тайла */ }
                    }, token));
                }
            }
            // Ждем завершения загрузки всех видимых тайлов (или сколько успеем до отмены)
            await Task.WhenAll(downloadTasks).ConfigureAwait(false);
        }

        private List<Uri> CalculateRequiredTiles(Envelope viewport)
        {
            // Логика расчета координат тайлов (X, Y, Zoom) на основе Bounding Box
            return new List<Uri>();
        }

        private void RenderPointsOnGui(IList<T> points)
        {
            // Данный метод гарантированно выполняется в UI-потоке.
            // Передаем массив графическому движку (например, через Direct2D / SkiaSharp / WriteableBitmap)
        }


    }
}
