using System;
using System.Reflection;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// Measures managed allocations on the current thread. Bound by reflection because
    /// <c>GC.GetAllocatedBytesForCurrentThread</c> is not in every Unity profile; where it is missing the
    /// calling test is ignored rather than failing to compile.
    /// </summary>
    public static class AllocationProbe
    {
        public static long BytesAllocatedBy(Action warmUpAndMeasure, int iterations)
        {
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
            for (int i = 0; i < 16; i++)
            {
                warmUpAndMeasure();
            }

            long before = counter();
            for (int i = 0; i < iterations; i++)
            {
                warmUpAndMeasure();
            }

            long after = counter();
            return after - before;
        }
    }
}
