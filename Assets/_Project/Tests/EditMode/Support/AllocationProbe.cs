using System;
using System.Reflection;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// Measures managed allocations on the current thread. Under .NET (the dotnet harness) it reads
    /// <c>GC.GetAllocatedBytesForCurrentThread</c> and returns bytes. Unity's Mono has that method
    /// but it always reports 0, so inside Unity the probe counts <c>GC.Alloc</c> profiler samples
    /// on this thread instead and returns how many allocations happened. Either way 0 means "no
    /// allocation" and a positive number means "allocated", which is all the tests rely on.
    /// </summary>
    public static class AllocationProbe
    {
        public static long BytesAllocatedBy(Action warmUpAndMeasure, int iterations)
        {
            for (int i = 0; i < 16; i++)
            {
                warmUpAndMeasure();
            }

#if UNITY_5_3_OR_NEWER
            UnityEngine.Profiling.Recorder recorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    warmUpAndMeasure();
                }
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }

            return recorder.sampleBlockCount;
#else
            MethodInfo method = typeof(GC).GetMethod(
                "GetAllocatedBytesForCurrentThread",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
            {
                Assert.Ignore("This runtime cannot count allocations per thread.");
            }

            var counter = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            long before = counter();
            for (int i = 0; i < iterations; i++)
            {
                warmUpAndMeasure();
            }

            long after = counter();
            return after - before;
#endif
        }
    }
}
