/** 
 * Copyright (c) 2024-26 Igor Shkabar. All rights reserved.
 * A high-performance streaming R-Tree spatial index framework featuring zero-allocation 
 * support and SIMD acceleration, designed to operate via memory-mapped files (MMF).* 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/* используем unsafe struct SimdAABB для быстрой предварительной фильтрации и гарантируем 
 * потокобезопасность чтения с помощью ReaderWriterLockSlim (если дерево может перестраиваться 
 * на лету). */
namespace Architecture.GIS.StreamingRTree
{
    // SIMD-accelerated packed Axis-Aligned Bounding Box (AABB).
    // Structure size: 16 bytes (4 float values).

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct SimdAABB
    {
        // structure`s data: Data fields (strictly 16 Bytes)
        private float min_X;
        private float min_Y;
        private float max_X;
        private float max_Y;

        public float MinX { get => min_X; set { min_X = value; } }
        public float MinY { get => min_Y; set { min_Y = value; } }
        public float MaxX { get => max_X; set { max_X = value; } }
        public float MaxY { get => max_Y; set { max_Y = value; } }

        private const float HalfScale = 0.5f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SimdAABB(float mX, float mY, float mxX, float mxY)
        {
            min_X = mX;
            min_Y = mY;
            max_X = mxX;
            max_Y = mxY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(in SimdAABB other)
        {
            return !(min_X > other.max_X || max_X < other.min_X ||
                        min_Y > other.max_Y || max_Y < other.min_Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float x, float y)
        {
            return x >= min_X && x <= max_X && y >= min_Y && y <= max_Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SimdAABB Merge(in SimdAABB other)
        {
            return new SimdAABB(
                min_X < other.min_X ? min_X : other.min_X,
                min_Y < other.min_Y ? min_Y : other.min_Y,
                max_X > other.max_X ? max_X : other.max_X,
                max_Y > other.max_Y ? max_Y : other.max_Y);
        }

        public float Area
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (max_X - min_X) * (max_Y - min_Y);
        }

        // Auxiliary properties
        public float Width => max_X - min_X;
        public float Height => max_Y - min_Y;
        public float CenterX => (min_X + max_X) * HalfScale;
        public float CenterY => (min_Y + max_Y) * HalfScale;

#if NET48
    // Код только для классического .NET Framework (4.8 и ниже)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Numerics.Vector<float> AsVector4()
    {            
        fixed (float* ptr = &min_X)
        {
            // Using System.Runtime.CompilerServices
             return
                System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Numerics.Vector<float>>(ptr);
        }
    }
#elif NET8_0_OR_GREATER
    // Код для современного .NET (5.0, 6.0, 7.0, 8.0, 9.0+)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public System.Runtime.Intrinsics.Vector128<float> AsVector128()
    {
        return System.Runtime.Intrinsics.Vector128.Create(min_X, min_Y, max_X, max_Y);
    }  
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SimdAABB Inflate(float delta)
        {
            float expandedMinX = min_X - delta;
            float expandedMinY = min_Y - delta;
            float expandedMaxX = max_X + delta;
            float expandedMaxY = max_Y + delta;

            return new SimdAABB(expandedMinX, expandedMinY, expandedMaxX, expandedMaxY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SimdAABB Intersection(in SimdAABB other)
        {
            float intersectMinX = min_X > other.min_X ? min_X : other.min_X;
            float intersectMinY = min_Y > other.min_Y ? min_Y : other.min_Y;
            float intersectMaxX = max_X < other.max_X ? max_X : other.max_X;
            float intersectMaxY = max_Y < other.max_Y ? max_Y : other.max_Y;

            return new SimdAABB(intersectMinX, intersectMinY, intersectMaxX, intersectMaxY);
        }


        public float Perimeter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Width + Height) * 2.0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float DistanceSquaredTo(float x, float y)
        {
            float deltaX = 0.0f;
            float deltaY = 0.0f;

            if (x < min_X)
            {
                deltaX = min_X - x;
            }
            else if (x > max_X)
            {
                deltaX = x - max_X;
            }

            if (y < min_Y)
            {
                deltaY = min_Y - y;
            }
            else if (y > max_Y)
            {
                deltaY = y - max_Y;
            }

            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetUnionArea(in SimdAABB other)
        {
            float unionMinX = min_X < other.min_X ? min_X : other.min_X;
            float unionMinY = min_Y < other.min_Y ? min_Y : other.min_Y;
            float unionMaxX = max_X > other.max_X ? max_X : other.max_X;
            float unionMaxY = max_Y > other.max_Y ? max_Y : other.max_Y;

            return (unionMaxX - unionMinX) * (unionMaxY - unionMinY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = min_X;
            minY = min_Y;
            maxX = max_X;
            maxY = max_Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SimdAABB other)
        {
            return min_X.Equals(other.min_X) &&
                   min_Y.Equals(other.min_Y) &&
                   max_X.Equals(other.max_X) &&
                   max_Y.Equals(other.max_Y);
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => max_X >= min_X && max_Y >= min_Y;
        }
    }
}
