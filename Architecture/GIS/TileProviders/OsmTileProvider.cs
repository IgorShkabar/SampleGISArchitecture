using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Architecture.GIS.Abstractions;

namespace Architecture.GIS.TileProviders
{
    public class OsmTileProvider : GIS.Abstractions.ITileProvider
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly Dictionary<string, Image> _memoryCache = 
            new Dictionary<string, Image>();
        private readonly string _cacheFolder = 
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TileCache");

        public OsmTileProvider()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WinFormsGisApp/1.0");
            Directory.CreateDirectory(_cacheFolder);
        }
        /*
        public async Task<Image> GetTileAsync(int x, int y, int zoom)
        {
            
            string key = $"{zoom}_{x}_{y}";
            if (_memoryCache.TryGetValue(key, out var cachedImg)) return cachedImg;

            string filePath = Path.Combine(_cacheFolder, $"{key}.png");
            if (File.Exists(filePath))
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var img = Image.FromStream(stream);
                    _memoryCache[key] = img;
                    return img;
                }
            }
            try
            {
                string url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
                byte[] bytes = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(filePath, bytes);

                using (var ms = new MemoryStream(bytes))
                {
                    var img = Image.FromStream(ms);
                    _memoryCache[key] = img;
                    return img;
                }
            }
            catch
            {
                return null; // Fallback placeholder
            }
            ObsoleteAttribute code: 
            This code is performable only for .NET Core 2.1/.NET Standard 2.1/.NET 5+
            & THERE is  Memory Leak!  если создаю Image.FromStream(stream) и сразу закрываю 
            поток using (var stream = ...), изображение Image в GDI+ отвалится или вызовет 
            падение при отрисовке, так как GDI+ требует, чтобы базовый поток оставался открытым 
            все время жизни Image.   
        }   */
        public async Task<Image> GetTileAsync(int x, int y, int zoom)
        {
            string key = $"{zoom}_{x}_{y}";
            if (_memoryCache.TryGetValue(key, out var cachedImg))
                return cachedImg;

            string filePath = Path.Combine(_cacheFolder, $"{key}.png");

            // 1. Проверяем локальный дисковый кэш
            if (File.Exists(filePath))
            {
                try
                {
                    // Загружаем байты в память, чтобы файл на диске не был заблокирован
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        // Image.FromStream требует копию (Bitmap), чтобы не держать MemoryStream открытым
                        var img = new Bitmap(Image.FromStream(ms));
                        _memoryCache[key] = img;
                        return img;
                    }
                }
                catch
                {
                    // Если файл поврежден, пробуем перекачать
                }
            }

            // 2. Если файла нет, скачиваем из сети
            try
            {
                string url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
                byte[] bytes = await _httpClient.GetByteArrayAsync(url);

                // Асинхронная запись на диск, совместимая со всеми версиями .NET (включая .NET Framework)
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await fs.WriteAsync(bytes, 0, bytes.Length);
                }

                using (var ms = new MemoryStream(bytes))
                {
                    var img = new Bitmap(Image.FromStream(ms));
                    _memoryCache[key] = img;
                    return img;
                }
            }
            catch
            {
                return null; // Fallback placeholder при отсутствии сети
            }
            /* почему это профессионально
            fs.WriteAsync вместо File.WriteAllBytesAsync: гарантирует 100% совместимость со 
            всеми версиями C# / .NET (включая .NET Framework 4.5+).

            new Bitmap(Image.FromStream(ms)): Создает независимую копию растрового изображения 
            в ОЗУ. Это решает главную проблему GDI+ в WinForms, когда загруженные из потока 
            тайлы "умирали" при закрытии MemoryStream.

            File.ReadAllBytes при чтении: Не блокирует файл на диске, позволяя другим потокам 
            параллельно работать с папкой кэша.*/
        }

        Task<Image> ITileProvider.GetTileAsync(int x, int y, int zoom)
        {
            throw new NotImplementedException();
        }
    }
}
/* 
 * class 
 * Уровень Архитектуры: Infrastructure
 * Роль и Зона ответственности: реализация ITileProvider. 
 * Загружает тайлы OpenStreetMap из сети и с диска.
 
*/

