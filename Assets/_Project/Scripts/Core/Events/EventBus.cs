// EventBus.cs
// 게임 내 도메인 이벤트 발행/구독을 위한 정적 EventBus.
// 명세: docs/event-bus-catalog.md

using System;
using System.Collections.Generic;

namespace FMLite.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
            {
                foreach (var d in existing.GetInvocationList())
                {
                    if (d.Equals(handler))
                        return;
                }
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
                return;

            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var existing))
                return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
                _handlers.Remove(type);
            else
                _handlers[type] = remaining;
        }

        public static void Publish<T>(T evt)
        {
            if (_handlers.TryGetValue(typeof(T), out var existing))
            {
                ((Action<T>)existing).Invoke(evt);
            }
        }

        public static void Clear()
        {
            _handlers.Clear();
        }
    }
}
