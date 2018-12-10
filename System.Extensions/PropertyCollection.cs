
namespace System.Extensions
{
    using System.Collections.Generic;
    public class Property<T>
    {
        //PropertyDescriptor
        static Property()
        {
            Properties = new List<Property<T>>(8);
        }
        internal readonly static List<Property<T>> Properties;//can Reflection

        public Property(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            var properties = Properties;
            lock (properties)
            {
                Index = properties.Count;
                _name = name;
                properties.Add(this);
            }
        }

        internal int Index;
        private string _name;
        public string Name => _name;
    }
    public class PropertyCollection<T>
    {
        public PropertyCollection()
        {
            var count = Property<T>.Properties.Count;
            _values = new object[count > 8 ? count : 8];
        }

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
        public void Clear(Predicate<Property<T>> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            var properties = Property<T>.Properties;
            var count = properties.Count > _values.Length ? _values.Length : properties.Count;
            for (int i = 0; i < count; i++)
            {
                if (match(properties[i]))
                {
                    _values[i] = null;
                }
            }
        }
        public void Clear()
        {
            Array.Clear(_values, 0, _values.Length);
        }
    }
}
