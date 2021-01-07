
namespace System
{
    using System.Diagnostics;
    using System.Collections.Generic;
    public class Property<T>
    {
        //PropertyDescriptor
        static Property()
        {
            _Properties = Array.Empty<Property<T>>();
        }

        private readonly static object _Sync = new object();
        internal static Property<T>[] _Properties;//CopyOnWrite
        public static IReadOnlyList<Property<T>> Properties => _Properties;
        public Property(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (name.Length == 0)
                throw new ArgumentException(nameof(name));

            lock (_Sync)
            {
                var properties = _Properties;
                _name = name;
                Index = properties.Length;
                Array.Resize(ref properties, Index + 1);
                properties[Index] = this;
                _Properties = properties;
            }
        }

        internal int Index;
        private string _name;
        public string Name => _name;
    }
    [DebuggerDisplay("Count = {DebugCount} , <{Property<T>.Properties.Count}>")]
    public class PropertyCollection<T>
    {
        public PropertyCollection()
        {
            var count = Property<T>.Properties.Count;
            _values = new object[count > 8 ? count : 8];
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private object[] _values;
        public object this[Property<T> property]
        {
            get
            {
                var index = property.Index;
                if (index >= _values.Length)
                    return null;

                return _values[index];
            }
            set
            {
                var index = property.Index;
                if (index >= _values.Length)
                {
                    if (value == null)
                        return;
                    //Array.Resize(ref _values, Property<T>.Properties.Count);
                    object[] newValues = new object[Property<T>.Properties.Count];
                    Array.Copy(_values, newValues, _values.Length);
                    _values = newValues;
                }
                _values[index] = value;
            }
        }
        public void Clear()
        {
            Array.Clear(_values, 0, _values.Length);
        }
        public void Remove(Predicate<Property<T>> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            var properties = Property<T>._Properties;
            var count = properties.Length > _values.Length ? _values.Length : properties.Length;
            for (int i = 0; i < count; i++)
            {
                if (_values[i] == null)
                    continue;
                if (match(properties[i]))
                {
                    _values[i] = null;
                }
            }
        }
        #region DebugView
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private KeyValuePair<string, object>[] DebugItems
        {
            get
            {
                var properties = Property<T>.Properties;
                var values = _values;
                var items = new KeyValuePair<string, object>[properties.Count];
                for (int i = 0; i < properties.Count; i++)
                {
                    var name = properties[i].Name;
                    var value = i < values.Length ? values[i] : null;
                    items[i] = new KeyValuePair<string, object>(name, value);
                }
                return items;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int DebugCount
        {
            get 
            {
                var values = _values;
                var count = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] != null)
                        count += 1;
                }
                return count;
            }
        }
        #endregion
    }
}
