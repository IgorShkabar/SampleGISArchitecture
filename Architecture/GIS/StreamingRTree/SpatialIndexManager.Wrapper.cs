/** 
 Copyright (c) 2024-26 Igor Shkabar. All rights reserved.
 Для реализации высокопроизводительного пространственного поиска на 100 000+ точках без 
 блокировки GUI-потока необходимо решить две задачи: обеспечивать потокобезопасный (thread-safe)
 быстрый поиск через R-Tree и выносить тяжелые операции (фильтрация, загрузка тайлов) в фоновые 
 потоки с использованием async/await и Task.Поскольку дерево STRtree из NetTopologySuite (судя 
 по Envelope) эффективно для статичных или редко меняющихся данных, стратегия заключается в 
 следующем: дерево строится один раз в фоновом потоке, а затем параллельно читается (метод Query)
 из разных потоков при изменении вьюпорта (Viewport/Bounding Box).
 */
 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Architecture.GIS.Abstractions;
using NetTopologySuite.Index.Strtree;

namespace Architecture.GIS.StreamingRTree
{
    public partial class SpatialIndexManager<T> : ISpatialIndex<T> where T : class
    {
        // Assuming STRtree is used as the underlying spatial index
        private readonly ISpatialIndex<T> _spatialIndex;
        private readonly STRtree<T> _rtree;
        private readonly System.Threading.ReaderWriterLockSlim _lock = new System.Threading.ReaderWriterLockSlim();
        private bool _isBuilt;

        public SpatialIndexManager()
        {
            // STRtree оптимизирован для построения "снизу вверх" (Sort-Tile-Recursive)
            _rtree = new STRtree<T>();
        }
        public SpatialIndexManager(ISpatialIndex<T> spatialIndex)
        {
            _spatialIndex = spatialIndex;
        }


        // Потокобезопасная вставка
        public void Insert(Envelope envelope, T item)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_isBuilt) throw new InvalidOperationException("Нельзя добавлять элементы в уже собранное STRtree без пересборки.");
                _rtree.Insert(envelope, item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Insert(T item, float x, float y)
        {
            Insert(new Envelope(x, x, y, y), item);
        }

        // Фиксация дерева (построение индекса)
        public void Build()
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_isBuilt)
                {
                    _rtree.Build();
                    _isBuilt = true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        // Многопоточный быстрый запрос к индексу
        public IList<T> Query(Envelope searchEnv)
        {
            _lock.EnterReadLock();
            try
            {
                // STRtree.Query потокобезопасен после вызова Build()
                return _rtree.Query(searchEnv);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // Высокопроизводительный метод фильтрации с использованием unsafe SimdAABB (параллельный поиск)
        public void QuerySimdParallel(in SimdAABB searchBounds, Func<T, SimdAABB> getBoundsBoundsFunc, List<T> resultBuffer)
        {
            // Конвертируем в Envelope для NTS STRtree
            var env = new Envelope(searchBounds.MinX, searchBounds.MaxX, searchBounds.MinY, searchBounds.MaxY);

            IList<T> candidates;
            _lock.EnterReadLock();
            try
            {
                candidates = _rtree.Query(env);
            }
            finally
            {
                _lock.ExitReadLock();
            }
            object syncLock = new object();

            // Создаем локальную копию структуры для безопасного использования в лямбде
            SimdAABB localSearchBounds = searchBounds;

            System.Threading.Tasks.Parallel.ForEach(
                candidates,
                () => new List<T>(),
                (item, state, localList) =>
                {
                    SimdAABB itemBox = getBoundsBoundsFunc(item);

            // Используем локальную копию вместо in-параметра
            if (localSearchBounds.Intersects(in itemBox))
                    {
                        localList.Add(item);
                    }
                    return localList;
                },
                (localList) =>
                {
                    lock (syncLock)
                    {
                        resultBuffer.AddRange(localList);
                    }
                }
            );
        }

        public IEnumerable<T> Search(double minX, double minY, double maxX, double maxY)
        {
            // Логика фильтрации R-Tree
            return new List<T>();
        }

        // Searches the spatial index for items matching the specified viewport.        
        // <param name="currentViewport"> The bounding envelope to search within.</param>
        // <returns> A list of spatial items found within the viewport.</returns>
        public int Search(Envelope currentViewport)
        {
            // 1. Guard against null arguments
            if (currentViewport == null)
            {
                return new int();
            }

            // 2. Delegate the search query to the underlying NTS spatial index
            return _spatialIndex.Search(currentViewport);
        }


        public void Clean()
        {
            _lock.EnterWriteLock();
            try
            {
                // Полная очистка структуры данных
                _isBuilt = false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        int ISpatialIndex<T>.Search(Envelope currentViewport)
        {
            return _spatialIndex.Search(currentViewport);
        }
    }
}
/* Разделение фаз чтения/записи (ReaderWriterLockSlim): В процессе интерактивной работы с картой (зум/пан) дерево блокируется только на чтение (EnterReadLock). Это позволяет множеству потоков параллельно опрашивать индекс.Паттерн Отмены (Cancellation Pattern): При непрерывном скроллинге карты GUI генерирует десятки событий в секунду. С помощью _cts.Cancel() мы мгновенно убиваем тяжелые фоновые вычисления и сетевые запросы тайлов, которые пользователь уже "упустил" из экрана.ConfigureAwait(false): Во внутренних асинхронных методах (FetchMapTilesAsync) контекст синхронизации отключается. Сетевой стек не пытается вернуться в GUI поток, снижая нагрузку на процессор до минимума. */
