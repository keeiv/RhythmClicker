using System;
using System.Collections.Generic;

namespace ClickerGame
{
    // Simple generic object pool.
    public class ObjectPool<T> where T : class
    {
        readonly Stack<T> _stack = new Stack<T>();
        readonly Func<T> _factory;

        public ObjectPool(Func<T> factory, int initial = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            for (int i = 0; i < initial; i++) _stack.Push(_factory());
        }

        public T Rent()
        {
            return _stack.Count > 0 ? _stack.Pop() : _factory();
        }

        public void Return(T item)
        {
            if (item == null) return;
            _stack.Push(item);
        }
    }
}
