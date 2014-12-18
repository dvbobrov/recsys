using System.Collections.Generic;

namespace svd
{
    internal class IdMap
    {
        private readonly Dictionary<long, int> _items;

        public IdMap(int capacity)
        {
            _items = new Dictionary<long, int>(capacity);
        }

        public int GetOrInsert(long itemId)
        {
            int id;
            if (!_items.TryGetValue(itemId, out id))
            {
                id = _items[itemId] = _items.Count;
            }
            return id;
        }

        public int this[long itemId]
        {
            get
            {
                int id;
                if (_items.TryGetValue(itemId, out id))
                {
                    return id;
                }
                return -1;
            }
        }

        public int Count => _items.Count;
    }
}