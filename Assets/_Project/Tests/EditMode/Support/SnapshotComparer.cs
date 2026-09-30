using System;
using System.Reflection;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// Deep equality and deep copy of plain DTOs over their public instance fields only: exactly what a
    /// field-based serializer such as JsonUtility would keep. Copying through it proves a snapshot carries
    /// all state in public fields.
    /// </summary>
    public static class SnapshotComparer
    {
        public static bool AreEqual(object a, object b)
        {
            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            Type type = a.GetType();
            if (type != b.GetType())
            {
                return false;
            }

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            {
                return a.Equals(b);
            }

            if (type.IsArray)
            {
                var left = (Array)a;
                var right = (Array)b;
                if (left.Length != right.Length)
                {
                    return false;
                }

                for (int i = 0; i < left.Length; i++)
                {
                    if (!AreEqual(left.GetValue(i), right.GetValue(i)))
                    {
                        return false;
                    }
                }

                return true;
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!AreEqual(field.GetValue(a), field.GetValue(b)))
                {
                    return false;
                }
            }

            return true;
        }

        public static T Clone<T>(T source)
            where T : class
        {
            return (T)CloneObject(source);
        }

        private static object CloneObject(object source)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            {
                return source;
            }

            if (type.IsArray)
            {
                var array = (Array)source;
                Array copy = Array.CreateInstance(type.GetElementType(), array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    copy.SetValue(CloneObject(array.GetValue(i)), i);
                }

                return copy;
            }

            object clone = Activator.CreateInstance(type);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                field.SetValue(clone, CloneObject(field.GetValue(source)));
            }

            return clone;
        }
    }
}
