// Интеграция и рендеринг в GUI-потокеГлавное правило: Поиск и скачивание происходят в фоне, отрисовка — в GUI-потоке.

// Метод вызывается при изменении Viewport (Скролл/Зум) в UI
private async void OnMapViewportChanged(Envelope currentViewport)
{
    // 1. Отменяем предыдущие незавершенные загрузки тайлов
    _cancellationTokenSource?.Cancel();
    _cancellationTokenSource = new CancellationTokenSource();

    try
    {
        // 2. Быстрый неблокирующий поиск 100k+ точек в R-Tree
        IList<MyPointClass> visiblePoints = await _spatialIndex.QueryViewportAsync(currentViewport);

        // 3. Параллельный запуск асинхронной загрузки необходимых тайлов
        List<Task<byte[]>> tileTasks = new List<Task<byte[]>>();
        foreach (var tileCoords in GetRequiredTiles(currentViewport))
        {
            tileTasks.Add(_tileDownloader.DownloadTileAsync(tileCoords.X, tileCoords.Y, tileCoords.Z, _cancellationTokenSource.Token));
        }

        // Ждем загрузки всех тайлов в фоновом режиме
        byte[][] loadedTiles = await Task.WhenAll(tileTasks);

        // 4. ВОЗВРАТ В GUI ПОТОК ДЛЯ ОТРИСОВКИ
        // Вызываем метод рендеринга графического движка (WPF/WinForms/Avalonia/MonoGame)
        RenderMap(visiblePoints, loadedTiles);
    }
    catch (OperationCanceledException)
    {
        // Игнорируем отмену операций
    }
}

.ConfigureAwait(false) гарантирует, что сетевые операции и парсинг дерева не будут пытаться 
захватить контекст синхронизации UI.Task.Run разгружает процессор: обход R-Tree над 100 000+ 
элементов занимает микросекунды, но на слабом CPU это может вызвать микрофризы UI, если делать 
в основном потоке.SocketsHttpHandler эффективно управляет пулом TCP-соединений, устраняя утечки
дескрипторов ОС.

Архитектурное решение
    Правило распределения потоков:Поток пула (Task.Run): Пространственный поиск по R-Tree и асинхронное скачивание тайлов.GUI-поток (Dispatcher / Invoke): 
    Только финальная отрисовка отфильтрованных точек и готовых тайлов на холсте.


private readonly SpatialIndexManager<MyPointDataType> _spatialIndex;
private CancellationTokenSource? _viewportCts;

private async void OnMapViewportChanged(Envelope currentViewport)
{
    // 1. Отменяем предыдущий поиск/загрузку, если пользователь быстро двигает карту (Debounce/Throttle)
    _viewportCts?.Cancel();
    _viewportCts = new CancellationTokenSource();
    var cancellationToken = _viewportCts.Token;

    try
    {
        // Переносим тяжелые вычисления в фоновый поток (ThreadPool)
        var (visiblePoints, mapTiles) = await Task.Run(async () =>
        {
            // 2. Пространственный поиск по R-Tree (выбираем только то, что в экране)
            // Предполагаем, что у SpatialIndexManager есть метод Search(Envelope env)
            var points = _spatialIndex.Search(currentViewport);

            // Проверяем, не изменился ли viewport, пока мы искали
            cancellationToken.ThrowIfCancellationRequested();

            // 3. Асинхронная фоновая загрузка тайлов (без блокировки UI)
            var tiles = await _tileDownloadService.GetTilesForViewportAsync(currentViewport, cancellationToken);

            return (points, tiles);
        }, cancellationToken);

        // 4. Возвращаемся в GUI-поток (благодаря await) и рендерим результат
        // Графический движок теперь обрабатывает только отфильтрованные данные
        RenderMap(visiblePoints, mapTiles);
    }
    catch (OperationCanceledException)
    {
        // Игнорируем отмену — пользователь просто скроллит дальше
    }
    catch (Exception ex)
    {
        // Логирование остальных ошибок (например, сети при загрузке тайлов)
        Logger.LogError(ex, "Ошибка при обновлении вьюпорта карты");
    }
}

private void RenderMap(IEnumerable<MyPointDataType> points, IEnumerable<Tile> tiles)
{
    // ВАЖНО: Этот метод выполняется строго в GUI-потоке.
    // Очищаем старые объекты и рисуем только те, что попали в Bounding Box.
    _mapCanvas.Clear();

    foreach (var tile in tiles)
    {
        _mapCanvas.DrawTile(tile);
    }

    foreach (var point in points)
    {
        _mapCanvas.DrawPoint(point);
    }
}

CancellationTokenSource(Отмена задач) : Когда пользователь зумит или двигает карту, событие OnMapViewportChanged срабатывает десятки раз в секунду.
    Старые фоновые запросы к R-Tree и серверу тайлов мгновенно отменяются через _viewportCts.Cancel(), не забивая пул потоков и сеть устаревшими данными.
    
    Task.Run: Гарантирует, что метод _spatialIndex.Search (который для 100k+ точек работает быстро, но все же нагружает CPU) не украдет ни одного миллисекунды у GUI-потока, предотвращая микрофризы("заикания") интерфейса.
    
    Потокобезопасность R-Tree: Убедитесь, что чтение(_spatialIndex.Search) потокобезопасно, если в этот момент из другого потока может происходить запись/динамическое добавление новых точек в индекс.Обычно для этого внутри SpatialIndexManager используется ReaderWriterLockSlim.