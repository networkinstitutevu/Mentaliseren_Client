using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System.Collections;

internal class UnsafeListTests : CollectionsTestCommonBase
{
    [Test]
    public unsafe void UnsafeList_Init_ClearMemory()
    {
        UnsafeList list = new UnsafeList(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(), 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        for (var i = 0; i < list.Length; ++i)
        {
            Assert.AreEqual(0, UnsafeUtility.ReadArrayElement<int>(list.Ptr, i));
        }

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_Allocate_Deallocate_Read_Write()
    {
        var list = new UnsafeList(Allocator.Persistent);

        list.Add(1);
        list.Add(2);

        Assert.AreEqual(2, list.Length);
        Assert.AreEqual(1, UnsafeUtility.ReadArrayElement<int>(list.Ptr, 0));
        Assert.AreEqual(2, UnsafeUtility.ReadArrayElement<int>(list.Ptr, 1));

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_Resize_ClearMemory()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 5, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        list.SetCapacity<int>(32);
        var capacity = list.Capacity;

        list.Resize(sizeOf, alignOf, 5, NativeArrayOptions.UninitializedMemory);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        for (var i = 0; i < 5; ++i)
        {
            UnsafeUtility.WriteArrayElement(list.Ptr, i, i);
        }

        list.Resize(sizeOf, alignOf, 10, NativeArrayOptions.ClearMemory);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        for (var i = 0; i < 5; ++i)
        {
            Assert.AreEqual(i, UnsafeUtility.ReadArrayElement<int>(list.Ptr, i));
        }

        for (var i = 5; i < list.Length; ++i)
        {
            Assert.AreEqual(0, UnsafeUtility.ReadArrayElement<int>(list.Ptr, i));
        }

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_Resize_Zero()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 5, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var capacity = list.Capacity;

        list.Add(1);
        list.Resize<int>(0);
        Assert.AreEqual(0, list.Length);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        list.Add(2);
        list.Clear();
        Assert.AreEqual(0, list.Length);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_TrimExcess()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        using (var list = new UnsafeList(sizeOf, alignOf, 32, Allocator.Persistent, NativeArrayOptions.ClearMemory))
        {
            var capacity = list.Capacity;

            list.Add(1);
            list.TrimExcess<int>();
            Assert.AreEqual(1, list.Length);
            Assert.AreEqual(1, list.Capacity);

            list.RemoveAtSwapBack<int>(0);
            Assert.AreEqual(list.Length, 0);
            list.TrimExcess<int>();
            Assert.AreEqual(list.Capacity, 0);

            list.Add(1);
            Assert.AreEqual(list.Length, 1);
            Assert.AreNotEqual(list.Capacity, 0);

            list.Clear();
        }
    }

    [Test]
    public unsafe void UnsafeList_DisposeJob()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 5, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        var disposeJob = list.Dispose(default);

        Assert.IsTrue(list.Ptr == null);

        disposeJob.Complete();
    }

    unsafe void Expected(ref UnsafeList list, int expectedLength, int[] expected)
    {
        Assert.AreEqual(0 == expectedLength, list.IsEmpty);
        Assert.AreEqual(list.Length, expectedLength);
        for (var i = 0; i < list.Length; ++i)
        {
            var value = UnsafeUtility.ReadArrayElement<int>(list.Ptr, i);
            Assert.AreEqual(expected[i], value);
        }
    }

    [Test]
    public unsafe void UnsafeList_AddNoResize()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        // List's capacity is always cache-line aligned, number of items fills up whole cache-line.
        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        Assert.Throws<Exception>(() => { fixed(int* r = range) list.AddRangeNoResize<int>(r, 17); });

        list.SetCapacity<int>(17);
        Assert.DoesNotThrow(() => { fixed(int* r = range) list.AddRangeNoResize<int>(r, 17); });

        list.SetCapacity<int>(16);
        Assert.Throws<Exception>(() => { list.AddNoResize(16); });
    }

    [Test]
    public unsafe void UnsafeList_AddNoResize_Read()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        list.AddNoResize(4);
        list.AddNoResize(6);
        list.AddNoResize(4);
        list.AddNoResize(9);
        Expected(ref list, 4, new int[] { 4, 6, 4, 9 });

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_RemoveAtSwapBack()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed(int* r = range) list.AddRange<int>(r, 10);
        list.RemoveAtSwapBack<int>(list.Length - 1);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        list.Clear();

        // test removing from the end
        fixed(int* r = range) list.AddRange<int>(r, 10);
        list.RemoveAtSwapBack<int>(5);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 9, 6, 7, 8 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_RemoveRangeSwapBackBE()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed(int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeSwapBackWithBeginEnd<int>(6, 9);
        Expected(ref list, 7, new int[] { 0, 1, 2, 3, 4, 5, 9 });
        list.Clear();

        // test removing all but one
        fixed(int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeSwapBackWithBeginEnd<int>(0, 9);
        Expected(ref list, 1, new int[] { 9 });
        list.Clear();

        // test removing from the front
        fixed(int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeSwapBackWithBeginEnd<int>(0, 3);
        Expected(ref list, 7, new int[] { 7, 8, 9, 3, 4, 5, 6 });
        list.Clear();

        // test removing from the middle
        fixed(int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeSwapBackWithBeginEnd<int>(0, 3);
        Expected(ref list, 7, new int[] { 7, 8, 9, 3, 4, 5, 6 });
        list.Clear();

        // test removing whole range
        fixed(int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeSwapBackWithBeginEnd<int>(0, 10);
        Expected(ref list, 0, new int[] { 0 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_RemoveAt()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed (int* r = range) list.AddRange<int>(r, 10);
        list.RemoveAt<int>(list.Length - 1);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        list.Clear();

        // test removing from the end
        fixed (int* r = range) list.AddRange<int>(r, 10);
        list.RemoveAt<int>(5);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 6, 7, 8, 9 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_RemoveRange()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed (int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeWithBeginEnd<int>(6, 9);
        Expected(ref list, 7, new int[] { 0, 1, 2, 3, 4, 5, 9 });
        list.Clear();

        // test removing all but one
        fixed (int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeWithBeginEnd<int>(0, 9);
        Expected(ref list, 1, new int[] { 9 });
        list.Clear();

        // test removing from the front
        fixed (int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeWithBeginEnd<int>(0, 3);
        Expected(ref list, 7, new int[] { 3, 4, 5, 6, 7, 8, 9 });
        list.Clear();

        // test removing from the middle
        fixed (int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeWithBeginEnd<int>(0, 3);
        Expected(ref list, 7, new int[] { 3, 4, 5, 6, 7, 8, 9 });
        list.Clear();

        // test removing whole range
        fixed (int* r = range) list.AddRange<int>(r, 10);
        list.RemoveRangeWithBeginEnd<int>(0, 10);
        Expected(ref list, 0, new int[] { 0 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_Remove_Throws()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        UnsafeList list = new UnsafeList(sizeOf, alignOf, 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        fixed (int* r = range) list.AddRange<int>(r, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveAt<int>(100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveAtSwapBack<int>(100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveRangeWithBeginEnd<int>(0, 100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveRangeSwapBackWithBeginEnd<int>(0, 100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentException>(() => { list.RemoveRangeWithBeginEnd<int>(100, 0);  });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentException>(() => { list.RemoveRangeSwapBackWithBeginEnd<int>(100, 0); });
        Assert.AreEqual(10, list.Length);

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeList_PtrLength()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        var list = new UnsafeList(sizeOf, alignOf, 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        fixed(int* r = range) list.AddRange<int>(r, 10);

        var listView = new UnsafeList((int*)list.Ptr + 4, 2);
        Expected(ref listView, 2, new int[] { 4, 5 });

        listView.Dispose();
        list.Dispose();
    }

    // Burst error BC1071: Unsupported assert type
    // [BurstCompile(CompileSynchronously = true)]
    struct UnsafeListParallelReader : IJob
    {
        public UnsafeList.ParallelReader list;

        public void Execute()
        {
            Assert.True(list.Contains(123));
        }
    }

    [Test]
    public void UnsafeList_ParallelReader()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        var list = new UnsafeList(sizeOf, alignOf, 10, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        list.Add(123);

        var job = new UnsafeListParallelReader
        {
            list = list.AsParallelReader(),
        };

        list.Dispose(job.Schedule()).Complete();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct UnsafeListParallelWriter : IJobParallelFor
    {
        public UnsafeList.ParallelWriter list;

        public void Execute(int index)
        {
            list.AddNoResize(index);
        }
    }

    [Test]
    public void UnsafeList_ParallelWriter()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        var list = new UnsafeList(sizeOf, alignOf, 256, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        var job = new UnsafeListParallelWriter
        {
            list = list.AsParallelWriter(),
        };

        job.Schedule(list.Capacity, 1).Complete();

        Assert.AreEqual(list.Length, list.Capacity);

        list.Sort<int>();

        for (int i = 0; i < list.Length; i++)
        {
            unsafe
            {
                var value = UnsafeUtility.ReadArrayElement<int>(list.Ptr, i);
                Assert.AreEqual(i, value);
            }
        }

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_IndexOf()
    {
        using (var list = new UnsafeList<int>(10, Allocator.Persistent) { 123, 789 })
        {
            bool r0 = false, r1 = false, r2 = false;

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                r0 = -1 != list.IndexOf(456);
                r1 = list.Contains(123);
                r2 = list.Contains(789);
            });

            Assert.False(r0);
            Assert.True(r1);
            Assert.True(r2);
        }
    }

    [Test]
    [Obsolete("Sort is being replaced by SortJob. (RemovedAfter 2021-06-20)", false)]
    public void UnsafeListT_GenericSort()
    {
        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(4 - i);
            }

            container.Sort();

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(i, container[i]);
            }
        }

        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(4 - i);
            }

            container.Sort(default).Complete();

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(i, container[i]);
            }
        }
    }

    [Test]
    public void UnsafeListT_GenericSortJob()
    {
        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(4 - i);
            }

            container.Sort();

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(i, container[i]);
            }
        }

        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(4 - i);
            }

            container.SortJob().Schedule().Complete();

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(i, container[i]);
            }
        }
    }

    struct DescendingComparer<T> : IComparer<T> where T : IComparable<T>
    {
        public int Compare(T x, T y) => y.CompareTo(x);
    }

    [Test]
    [Obsolete("Sort is being replaced by SortJob. (RemovedAfter 2021-06-20)", false)]
    public void UnsafeListT_GenericSortCustomComparer()
    {
        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(i);
            }

            container.Sort(new DescendingComparer<int>());

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(4 - i, container[i]);
            }
        }

        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(i);
            }

            container.Sort(new DescendingComparer<int>(), default).Complete();

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(4 - i, container[i]);
            }
        }
    }

    [Test]
    public void UnsafeListT_GenericSortJobCustomComparer()
    {
        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(i);
            }

            container.Sort(new DescendingComparer<int>());

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(4 - i, container[i]);
            }
        }

        using (var container = new UnsafeList<int>(5, Allocator.Persistent))
        {
            for (var i = 0; i < 5; ++i)
            {
                container.Add(i);
            }

            container.SortJob(new DescendingComparer<int>()).Schedule().Complete();

            for (var i = 0; i < 5; ++i)
            {
                Assert.AreEqual(4 - i, container[i]);
            }
        }
    }

    [Test]
    public void UnsafeListT_InsertRangeWithBeginEnd()
    {
        var list = new UnsafeList<byte>(3, Allocator.Persistent);
        list.Add(0);
        list.Add(3);
        list.Add(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => list.InsertRangeWithBeginEnd(-1, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.InsertRangeWithBeginEnd(0, 8));
        Assert.Throws<ArgumentException>(() => list.InsertRangeWithBeginEnd(3, 1));

        Assert.DoesNotThrow(() => list.InsertRangeWithBeginEnd(1, 3));

        list[1] = 1;
        list[2] = 2;

        for (var i = 0; i < 5; ++i)
        {
            Assert.AreEqual(i, list[i]);
        }

        list.Dispose();
    }

    [Test]
    public void UnsafeListT_BinarySearch()
    {
        using (var container = new UnsafeList<int>(16, Allocator.Persistent) { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53 })
        {
            for (int i = 0, num = container.Length; i < num; ++i)
            {
                Assert.AreEqual(i, container.BinarySearch(container[i]));
            }
        }
    }

    [Test]
    public void UnsafeListT_BinarySearch_NotFound()
    {
        {
            var container = new UnsafeList<int>(1, Allocator.Temp);
            Assert.AreEqual(-1, container.BinarySearch(1));

            container.Add(1);

            Assert.AreEqual( 0, container.BinarySearch( 1));
            Assert.AreEqual(-1, container.BinarySearch(-2));
            Assert.AreEqual(-2, container.BinarySearch( 2));
        }

        using (var container = new UnsafeList<int>(16, Allocator.Temp) { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })
        {
            for (int i = 0, num = container.Length; i < num; ++i)
            {
                Assert.AreEqual(~container.Length, container.BinarySearch(i + 16));
            }
        }

        using (var container = new UnsafeList<int>(8, Allocator.Temp) { 0, 2, 4, 6, 8, 10, 12, 14 })
        {
            for (int i = 0, num = container.Length; i < num; ++i)
            {
                Assert.AreEqual(~(i + 1) /* ~index of first greatest value searched */, container.BinarySearch(i * 2 + 1));
            }
        }
    }

#if !UNITY_DOTSRUNTIME
    [Test]
    public void UnsafeListT_BinarySearch_NotFound_Reference_ArrayList()
    {
        {
            var reference = new ArrayList();
            var container = new UnsafeList<int>(1, Allocator.Temp);
            Assert.AreEqual(-1, reference.BinarySearch(1));
            Assert.AreEqual(-1, container.BinarySearch(1));

            reference.Add(1);
            container.Add(1);

            Assert.AreEqual(0, reference.BinarySearch(1));
            Assert.AreEqual(0, container.BinarySearch(1));

            Assert.AreEqual(-1, reference.BinarySearch(-2));
            Assert.AreEqual(-1, container.BinarySearch(-2));

            Assert.AreEqual(-2, reference.BinarySearch(2));
            Assert.AreEqual(-2, container.BinarySearch(2));
        }

        using (var container = new UnsafeList<int>(16, Allocator.Temp) { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })
        {
            var reference = new ArrayList() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

            for (int i = 0, num = container.Length; i < num; ++i)
            {
                Assert.AreEqual(~reference.Count, reference.BinarySearch(i + 16));
                Assert.AreEqual(~container.Length, container.BinarySearch(i + 16));
            }
        }

        using (var container = new UnsafeList<int>(8, Allocator.Temp) { 0, 2, 4, 6, 8, 10, 12, 14 })
        {
            var reference = new ArrayList() { 0, 2, 4, 6, 8, 10, 12, 14 };

            for (int i = 0, num = container.Length; i < num; ++i)
            {
                Assert.AreEqual(~(i + 1) /* ~index of first greatest value searched */, reference.BinarySearch(i * 2 + 1));
                Assert.AreEqual(~(i + 1) /* ~index of first greatest value searched */, container.BinarySearch(i * 2 + 1));
            }
        }
    }
#endif

    [Test]
    public void UnsafeListT_ForEach([Values(10, 1000)]int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeList<int>(32, Allocator.TempJob))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i);
            }

            var count = 0;
            foreach (var item in container)
            {
                Assert.True(container.Contains(item));
                seen[item] = seen[item] + 1;
                ++count;
            }

            Assert.AreEqual(container.Length, count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect item count {i}");
            }
        }
    }
}
