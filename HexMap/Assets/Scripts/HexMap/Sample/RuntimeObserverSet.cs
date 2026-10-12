using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexMap.Sample
{
    internal sealed class RuntimeObserverSet<T>
    {
        private readonly List<Entry> m_Entries = new List<Entry>();
        private int m_PublishDepth;

        public IDisposable Subscribe(Action<T> observer, object target)
        {
            var entry = new Entry(observer, target);
            m_Entries.Add(entry);
            return new Subscription(this, entry);
        }

        public void Publish(T value)
        {
            m_PublishDepth++;
            try
            {
                var count = m_Entries.Count;
                for (var index = 0; index < count; index++)
                {
                    var entry = m_Entries[index];
                    if (entry.IsDisposed || IsDestroyedUnityTarget(entry.Target))
                    {
                        entry.IsDisposed = true;
                        continue;
                    }

                    try
                    {
                        entry.Observer(value);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                m_PublishDepth--;
                if (m_PublishDepth == 0)
                {
                    RemoveDisposedEntries();
                }
            }
        }

        public void Clear()
        {
            for (var index = 0; index < m_Entries.Count; index++)
            {
                m_Entries[index].IsDisposed = true;
            }

            if (m_PublishDepth == 0)
            {
                m_Entries.Clear();
            }
        }

        private void Unsubscribe(Entry entry)
        {
            entry.IsDisposed = true;
            if (m_PublishDepth == 0)
            {
                m_Entries.Remove(entry);
            }
        }

        private void RemoveDisposedEntries()
        {
            for (var index = m_Entries.Count - 1; index >= 0; index--)
            {
                if (m_Entries[index].IsDisposed)
                {
                    m_Entries.RemoveAt(index);
                }
            }
        }

        private static bool IsDestroyedUnityTarget(object target)
        {
            var unityTarget = target as UnityEngine.Object;
            return !ReferenceEquals(unityTarget, null) && unityTarget == null;
        }

        private sealed class Entry
        {
            public Entry(Action<T> observer, object target)
            {
                Observer = observer;
                Target = target;
            }

            public Action<T> Observer { get; }

            public object Target { get; }

            public bool IsDisposed { get; set; }
        }

        private sealed class Subscription : IDisposable
        {
            private RuntimeObserverSet<T> m_Owner;
            private Entry m_Entry;

            public Subscription(RuntimeObserverSet<T> owner, Entry entry)
            {
                m_Owner = owner;
                m_Entry = entry;
            }

            public void Dispose()
            {
                if (m_Owner == null)
                {
                    return;
                }

                m_Owner.Unsubscribe(m_Entry);
                m_Owner = null;
                m_Entry = null;
            }
        }
    }
}
