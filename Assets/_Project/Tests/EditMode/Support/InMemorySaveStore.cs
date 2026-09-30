using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Callbreak.Application.Ports;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// An <see cref="ISaveStore"/> in memory. Like the JSON store it keeps copies, not the objects
    /// it was given, and copies only public fields (what JsonUtility serializes); null strings,
    /// lists and arrays come back empty, as they do from JsonUtility. Tests can seed raw values
    /// (e.g. a file from a newer schema) with <see cref="Put"/> and count saves.
    /// </summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _saves = new Dictionary<string, int>(StringComparer.Ordinal);

        public int SaveCount { get; private set; }

        public int SavesOf(string key) => _saves.TryGetValue(key, out int n) ? n : 0;

        /// <summary>Stores a value as if an earlier session had saved it (not counted as a save).</summary>
        public void Put<T>(string key, T value) where T : class
        {
            _values[key] = Copy(value);
        }

        /// <summary>A copy of what is stored, or null.</summary>
        public T Peek<T>(string key) where T : class
        {
            return _values.TryGetValue(key, out object stored) ? (T)Copy(stored) : null;
        }

        public bool TryLoad<T>(string key, out T value) where T : class
        {
            if (_values.TryGetValue(key, out object stored) && stored is T)
            {
                value = (T)Copy(stored);
                return true;
            }

            value = null;
            return false;
        }

        public void Save<T>(string key, T value) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _values[key] = Copy(value);
            _saves[key] = SavesOf(key) + 1;
            SaveCount++;
        }

        public void Delete(string key)
        {
            _values.Remove(key);
        }

        public bool Exists(string key) => _values.ContainsKey(key);

        private static object Copy(object source)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
            {
                return source;
            }

            if (type.IsArray)
            {
                var array = (Array)source;
                Array copy = Array.CreateInstance(type.GetElementType(), array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    copy.SetValue(Copy(array.GetValue(i)), i);
                }

                return copy;
            }

            if (source is IList list && type.IsGenericType)
            {
                var copy = (IList)Activator.CreateInstance(type);
                foreach (object item in list)
                {
                    copy.Add(Copy(item));
                }

                return copy;
            }

            object result = Activator.CreateInstance(type, true);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsInitOnly || field.IsDefined(typeof(NonSerializedAttribute), false))
                {
                    continue;
                }

                object value = Copy(field.GetValue(source));
                if (value == null)
                {
                    value = EmptyValueFor(field.FieldType);
                }

                field.SetValue(result, value);
            }

            return result;
        }

        private static object EmptyValueFor(Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }

            if (type.IsArray)
            {
                return Array.CreateInstance(type.GetElementType(), 0);
            }

            if (type.IsGenericType && typeof(IList).IsAssignableFrom(type))
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }
    }
}
