/*
Copyright (c) 2011-2024 Bonsai Foundation CIC and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai;

internal static class ObservableCombinators
{
    /// <summary>
    /// Returns a connectable observable sequence that upon connection causes the <paramref name="source"/>
    /// to push results into a new fresh subject, which is created by invoking the specified
    /// <paramref name="subjectFactory"/>.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source sequence.</typeparam>
    /// <typeparam name="TResult">The type of the elements in the result sequence.</typeparam>
    /// <param name="source">The source sequence whose elements will be pushed into the specified subject.</param>
    /// <param name="subjectFactory">
    /// The factory function used to create the subject that notifications will be pushed into.
    /// </param>
    /// <returns>The reconnectable sequence.</returns>
    public static IConnectableObservable<TResult> MulticastReconnectable<TSource, TResult>(this IObservable<TSource> source, Func<ISubject<TSource, TResult>> subjectFactory)
    {
        return new ReconnectableObservable<TSource, TResult>(source, subjectFactory);
    }

    /// <summary>
    /// Returns a connectable observable sequence that upon connection causes the <paramref name="source"/>
    /// to push results into a new fresh <see cref="Subject{TSource}"/>.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence whose elements will be pushed into the specified subject.</param>
    /// <returns>The reconnectable sequence.</returns>
    public static IConnectableObservable<TSource> PublishReconnectable<TSource>(this IObservable<TSource> source)
    {
        return MulticastReconnectable(source, () => new Subject<TSource>());
    }
}

class ReconnectableObservable<TSource, TResult> : IConnectableObservable<TResult>
{
    readonly object gate;
    readonly IObservable<TSource> observableSource;
    readonly Func<ISubject<TSource, TResult>> subjectFactory;
    IConnectableObservable<TResult> connectableSource;

    public ReconnectableObservable(IObservable<TSource> source, Func<ISubject<TSource, TResult>> subjectSelector)
    {
        subjectFactory = subjectSelector;
        observableSource = source.AsObservable();
        gate = new object();
    }

    public IDisposable Connect()
    {
        lock (gate)
        {
            EnsureConnectableSource();
            var connection = connectableSource.Connect();
            return Disposable.Create(() =>
            {
                lock (gate)
                {
                    using (connection)
                    {
                        connectableSource = null;
                    }
                }
            });
        }
    }

    public IDisposable Subscribe(IObserver<TResult> observer)
    {
        lock (gate)
        {
            EnsureConnectableSource();
            return connectableSource.Subscribe(observer);
        }
    }

    private void EnsureConnectableSource()
    {
        if (connectableSource == null)
        {
            var subject = subjectFactory();
            connectableSource = observableSource.Multicast(subject);
        }
    }
}
