/** 
 Асинхронный Tile Downloader (Защита от Socket Exhaustion)
 создание new HttpClient() как в MapViewController.FetchMapTilesAsync(..){ using(_httpClient = new System.Net.Http.HttpClient()} 
 на каждый тайл убьет производительность системы из-за накопления портов в состоянии TIME_WAIT. 
 Для этого использу 1 разделяемый инстанс HttpClient с настроенным SocketsHttpHandler для контроля за жизненным циклом соединений
 */
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Architecture.GIS.Controllers
{
    public class MapTileDownloader
    {
        // Единственный инстанс на все приложение во избежание Socket Exhaustion
        private static readonly HttpClient _httpClient;

        static MapTileDownloader()
        {
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 20 // Ограничение параллельных запросов к одному хосту карт
            };
            _httpClient = new HttpClient(handler);
            // Заголовок обязателен для большинства серверов (OSM, Mapbox и т.д.)
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YourMapEngine/1.0 (C# App)");
        }

        // Асинхронная загрузка тайла без блокировки UI
        public async Task<byte[]> DownloadTileAsync(int x, int y, int zoom, CancellationToken cancellationToken)
        {
            string url = $"https://openstreetmap.org{zoom}/{x}/{y}.png";

            try
            {
                // Используем HttpCompletionOption.ResponseHeadersRead для минимизации аллокаций памяти
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) // В .NET 4.7.2 ReadAsStreamAsync часто без параметров
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await stream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
                            return memoryStream.ToArray();
                        }
                    }
                }

            }
            catch (HttpRequestException ex)
            {
                // Логирование ошибки сети (тайл не найден или сервер недоступен)
                return null;
            }
            catch (TaskCanceledException)
            {
                // Запрос был отменен (например, пользователь быстро скроллит карту и тайл вышел из экрана)
                return null;
            }
        }
    }
}
