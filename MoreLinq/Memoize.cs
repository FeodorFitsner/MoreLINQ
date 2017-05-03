#region License and Terms
// MoreLINQ - Extensions to LINQ to Objects
// Copyright (c) 2017 Leandro F. Vieira (leandromoh). All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

namespace MoreLinq
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    static partial class MoreEnumerable
    {
        /// <summary>
        /// Returns a <see cref="IEnumerable{T}"/> that lazily creates an in-memory
        /// cache of the enumeration on first iteration, if it is not already an
        /// in-memory source.
        /// </summary>
        /// <param name="source">The source sequence.</param>
        /// <returns>Returns a sequence that corresponds to a cached version of the input sequence.</returns>
        /// <remarks>
        /// The returned <see cref="IEnumerable{T}"/> will cache items from
        /// <paramref name="source"/> in a thread-safe manner such that it can
        /// be shared between threads. Each thread can call its
        /// <see cref="IEnumerable{T}.GetEnumerator"/> to acquire an iterator
        /// but the same iterator should not be used simultanesouly from
        /// multiple threads. The sequence supplied in <paramref name="source"/>
        /// is not expected to be thread-safe but it is required to be
        /// thread-agnostic because different threads (though never
        /// simultaneously) may iterate over the sequence.
        /// </remarks>

        public static IEnumerable<T> Memoize<T>(this IEnumerable<T> source)
        {
            return source.Memoize(false);
        }

        /// <summary>
        /// Returns a <see cref="IEnumerable{T}"/> that lazily creates an in-memory
        /// cache of the enumeration on first iteration, if it is not already an
        /// in-memory source.
        /// </summary>
        /// <param name="source">The source sequence.</param>
        /// <param name="disposeOnEarlyExit">Indicates if the call to dispose method of source's enumerator, 
        /// and therefore the close of the buffering, must happen at the end of the first iteration (true) 
        /// or only when source is entirely iterated (false).</param>
        /// <returns>Returns a sequence that corresponds to a cached version of the input sequence.</returns>
        /// <remarks>
        /// The returned <see cref="IEnumerable{T}"/> will cache items from
        /// <paramref name="source"/> in a thread-safe manner such that it can
        /// be shared between threads. Each thread can call its
        /// <see cref="IEnumerable{T}.GetEnumerator"/> to acquire an iterator
        /// but the same iterator should not be used simultanesouly from
        /// multiple threads. The sequence supplied in <paramref name="source"/>
        /// is not expected to be thread-safe but it is required to be
        /// thread-agnostic because different threads (though never
        /// simultaneously) may iterate over the sequence.
        /// </remarks>

        public static IEnumerable<T> Memoize<T>(this IEnumerable<T> source, bool disposeOnEarlyExit)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (source is ICollection<T>)
            {
                return source;
            }

            return (source as MemoizedEnumerable<T>) ?? new MemoizedEnumerable<T>(source, disposeOnEarlyExit);
        }
    }

    internal class MemoizedEnumerable<T> : IEnumerable<T>
    {
        private readonly IList<T> cache;
        private readonly bool disposeOnEarlyExit;
        private readonly object locker;
        private IEnumerable<T> source;
        private IEnumerator<T> sourceEnumerator;
        private bool disposed;

        public MemoizedEnumerable(IEnumerable<T> sequence, bool shouldDisposeOnEarlyExit)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));

            source = sequence;
            cache = new List<T>();
            disposeOnEarlyExit = shouldDisposeOnEarlyExit;
            locker = new object();
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (sourceEnumerator == null && !disposed)
                lock (locker)
                    if (sourceEnumerator == null && !disposed)
                        sourceEnumerator = source.GetEnumerator();

            var index = 0;
            var hasValue = false;

            try
            {
                while (true)
                {
                    lock (locker)
                    {
                        if (index < cache.Count)
                        {
                            hasValue = true;
                        }

                        else if ((hasValue = !disposed && sourceEnumerator.MoveNext()))
                        {
                            cache.Add(sourceEnumerator.Current);
                        }

                        else if (!disposed)
                        {
                            DisposeSourceResources();
                        }
                    }

                    if (hasValue)
                        yield return cache[index];
                    else
                        break;

                    index++;
                }
            }
            finally
            {
                if (!disposed && disposeOnEarlyExit)
                    lock (locker)
                        if (!disposed && disposeOnEarlyExit)
                            DisposeSourceResources();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void DisposeSourceResources()
        {
            disposed = true;
            sourceEnumerator.Dispose();
            source = null;
            sourceEnumerator = null;
        }
    }
}