﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

using System.Collections.Generic;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IObservable<TSource> ToObservable<TSource>(this IAsyncEnumerable<TSource> source)
        {
            if (source == null)
                throw Error.ArgumentNull(nameof(source));

            return new ToObservableObservable<TSource>(source);
        }

        private sealed class ToObservableObservable<T> : IObservable<T>
        {
            private readonly IAsyncEnumerable<T> _source;

            public ToObservableObservable(IAsyncEnumerable<T> source)
            {
                _source = source;
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                var ctd = new CancellationTokenDisposable();

                async void Core()
                {
                    // REVIEW: fire-and-forget DisposeAsync?
                    await using (var e = _source.GetAsyncEnumerator(ctd.Token))
                    {
                        do
                        {
                            bool hasNext;
                            try
                            {
                                hasNext = await e.MoveNextAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                if (!ctd.Token.IsCancellationRequested)
                                {
                                    observer.OnError(ex);
                                }

                                return;
                            }

                            if (!hasNext)
                            {
                                observer.OnCompleted();
                                return;
                            }

                            observer.OnNext(e.Current);
                        }
                        while (!ctd.Token.IsCancellationRequested);
                    }
                }

                // Fire and forget
                Core();

                return ctd;
            }
        }
    }
}
