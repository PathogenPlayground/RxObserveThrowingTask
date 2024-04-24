//==================================================================================================================
// General
//==================================================================================================================
#define ASYNC_OBSERVABLE // Use an async observable

//==================================================================================================================
// Control how the observable ends (pick one or none)
//==================================================================================================================
#define COMPLETE_NORMALLY // End the observable with OnCompleted
//#define ONERROR_OBSERVABLE // End the observable with an error explicitly
//#define THROWING_OBSERVABLE // Throw an exception in the observable
// (If none enabled: No explicit completion)

//==================================================================================================================
// Infinite sequence tests
//==================================================================================================================
//#define INFINITE_SEQUENCE // Make the observable sequence infinite (use one of the two to provide means of completion)
//#define ASYNC_OBSERVABLE_CANCEL // Cancel the sequence before it completes
//#define TAKE_3_ONLY // Take only 3 elements of the sequence (only makes sense with async observable)
using Bonsai;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;

Console.WriteLine($"Hello, World! Using Rx.NET version {typeof(System.Reactive.IObserver<,>).Assembly.GetName().Version}");

// Not applicable, it's the same between this and Bonsai (leaving here so I remember I checked it)
Console.WriteLine($"Observable impl: {typeof(Observable).GetField("s_impl", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)}");

void ObservableFunc(IObserver<int> observer, CancellationToken cancellationToken)
{
    Console.WriteLine("Observable task start...");

#if INFINITE_SEQUENCE
    for (int i = 0; !cancellationToken.IsCancellationRequested; i++)
            observer.OnNext(i);
#else
    observer.OnNext(1);
    observer.OnNext(2);
    observer.OnNext(3);
#endif

#if THROWING_OBSERVABLE
    Console.WriteLine("Observable is gonna go bang...");
    throw new Exception("BANG");
#elif ONERROR_OBSERVABLE
    Console.WriteLine("Observable emitting OnError...");
    observer.OnError(new Exception("ONERROR BANG"));
#elif COMPLETE_NORMALLY
    // Finally is still called if this is omitted, but the exception is never propagated !!!
    Console.WriteLine("Observable explicitly completing.");
    observer.OnCompleted();
#else
    Console.WriteLine("Observable ending without any explicit completion.");
#endif
}

#if ASYNC_OBSERVABLE
IObservable<int> source = Observable.Create<int>
(
    (observer, cancellationToken) =>
    {
        return Task.Factory.StartNew
        (
            () => ObservableFunc(observer, cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }
);
#else
IObservable<int> source = Observable.Create<int>((observer) =>
{
    ObservableFunc(observer, CancellationToken.None);
    return Disposable.Empty;
});
#endif

// These do not seem to affect the outcome
//source = source.PublishReconnectable().RefCount();

#if TAKE_3_ONLY
source = source.Take(3);
#endif

source = source.Finally(() =>
{
    Console.WriteLine("Finally gonna go bang...");
    throw new Exception("FINALLY WENT BANG"); // We should expect the debugger to complain about this
});

CancellationTokenSource cts = new();

Console.WriteLine("Subscribing...");
source.Subscribe
(
    unit => Console.WriteLine($"Next: '{unit}'"),
    ex => Console.Error.WriteLine($"Error: {ex}"),
    () => Console.WriteLine("Completed"),
    cts.Token
);
#if ASYNC_OBSERVABLE
Console.WriteLine("Subscribled");
Thread.Sleep(1000);
#if ASYNC_OBSERVABLE_CANCEL
Console.WriteLine("Canceling...");
cts.Cancel();
Console.WriteLine("Canceled");
Thread.Sleep(500);
#endif
#endif

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("Goodbye (Should not reach here due to exception during finally action)");
Console.ForegroundColor = ConsoleColor.Gray;
