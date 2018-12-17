# C# Promises #

[travis-ci-status]: https://img.shields.io/travis/AgeOfLearning/promises.svg
[nuget-status]: https://img.shields.io/nuget/vpre/Promises.svg

[![travis][travis-ci-status]](https://travis-ci.org/AgeOfLearning/promises)
[![nuget][nuget-status]](https://www.nuget.org/packages/Promises/)


Promises library for C# and Unity

```c#
DownloadSomething(path)
    .Then(OnDownloaded)
    .Catch(OnDownloadFailed);
```


# Installation #
This library can be installed via [NuGet Package](https://www.nuget.org/packages/Promises/). 

# Unity Installation #
To install in Unity, extract the NuGet package and import the included *.unitypackage.



# Documentation #

## Table Of Contents ##
* [Basic Setup](#basic-setup)
* [Usage - Inline](#usage-inline)
* [Usage - Methods with Optional Parameters](#usage-methods-with-optional-parameters)
* [Promise Chaining](#promise-chaining)
* [Error Handling](#error-handling)
* [Done()](#done)
* [Finally()](#finally)
* [All()](#all)
* [Sequence()](#sequence)
* [Any()](#any)
* [Promise Cancellation](#cancellation)
* [Failed() and Resolved()](#creating-failed-and-resolved-promise)
* [Progress Reporting](#progress-reporting)
* [Consumer-facing Promises](#consumer-facing-promises)
* [Example - Running a Coroutine](#example-running-a-coroutine)
* [Converting Between Promise Types](#converting-between-promise-types)

## Basic Setup ##
Promises return a value at a later time: asynchronously. You simply create an instance and return it from your method. Your async work will then trigger the result via promise.Resolve. You can fail the promise by calling promise.Fail which takes an Exception.

```c#
public IPromise<AssetBundle> GetBundle(string path)
{
     IPromise<AssetBundle> promise = new Promise<AssetBundle>();

     StartCoroutine(GetBundleCoroutine(path, promise);

     return promise;
}

private IEnumerator GetBundleCoroutine(string path, IPromise<AssetBundle> promise)
{
     yield return www;
    
     if(www.error != null)
     {
          promise.Resolve(www.assetBundle);
     }
     else
     {
          promise.Fail(new Exception("Failed to find bundle!"));
     }
}
```

## Usage - Inline #
You can return another promise inside of a delegate callback, or a simple Action for handling the value returned by the promise.

```c#
GetBundle("...")
.Catch(delegate(Exception e)
{
    Debug.LogErrorFormat("Could not find bundle: {0}", e);
})
.Then(delegate(AssetBundle bundle) 
{
    // do something with bundle...
});
```

## Usage - Methods with Optional Parameters ###
You can add up to 3 P1, P2, P3 property parameters to pass through into the method. This is useful in cases where you do not wish
to create an inline delegate to handle the callback for a promise.
```c#
GetBundle("...")
.Then(LogText, "myAssetPath");

public void LogText(AssetBundle bundle, string path)
{
    Debug.Log(bundle.LoadAsset<Texture>(path));
}

```

## Promise Chaining ##
You can chain to the Promise returned by a method either by returning the IPromise<> inside of a delegate or pass by reference.

```c#
WaitForSecondsPromise(5)
.Chain(GetBundle, "path/to/my/bundle")
.Chain(LogText, "myAssetPath");

private IPromise<Int> WaitForSecondsPromise(int seconds)
{
    IPromise<int> promise = new Promise<int>();
    StartCoroutine(WaitForSecondsCoroutine(promise, seconds);
    
    return promise;
}

private IEnumerator WaitForSecondsCoroutine(IPromise<int> promise, int seconds)
{
     yield return new WaitForSeconds(seconds);

     promise.Resolve(seconds);
}
```

## Error Handling ##
You can register exception handling callback by calling promise.Catch. You can either catch all exceptions using Catch() or Catch<System.Exception> method or you can register a callback that would be called when promise is failed with a specific type of exception. 

```c#
SendLoginRequest(username, password)
    .Then(OnLoginSuccess)
    .Catch(delegate(NotAuthorizedHttpException e){
        SetMessage("Login/password are incorrect");
    });
```

This code would be an equivalent of:

```c#
SendLoginRequest(username, password)
    .Then(OnLoginSuccess)
    .Catch(delegate(Exception e){
        if(e is NotAuthorizedHttpException)
        {
            SetMessage("Login/password are incorrect");
        }
    });
```

**Note:** Prior to version 1.2, Promises library provided a false expectation that catching a concrete exception type would block all other exception handlers from firing. This was fixed in 1.2.

When promise doesn't have any Catch handlers, it's exceptions are swallowed. To avoid it, you should either add a default exception handler or call Done() in the end of your chain. Done will add a default uncaught exception callback, which by default will re-throw an exception. You can change that by providing your global UncaughtExceptionThrown callback: 

```c#
Promise.UncaughtExceptionThrown += delegate(Exception e)
{
    Debug.LogException(e); // This also works in a separate thread
}
```

If you need to use promises along with threads, it is highly recommended to provide that handler, since separate threads don't get caught/prompted to the console by Unity.


## Done() ##
You can call Done() after your chain. Done serves 2 purposes:
1. Adds your final Then callback (optional)
2. Adds a default uncaught System.Exception handler. The default exception handler will be used if no Catch callback is provided for the type of exception that caused promise to fail.
The default handler will use UncaughtExceptionThrown event, or re-throw an exception if no UncaughtExceptionThrown event listeners exist.

**Best practice:** Unless you are returning a promise to a consumer code, either add a default Catch handler or call Done(). Otherwise, your promise exception will not be handled/printed.


## Finally() ##
Finally callback adds a handler that would get called if promise fails or resolves. This callback will be called after all Catch or Resolve callbacks are called.

```c#
LoadBundle(path)
    .Then(OnBundleLoaded)
    .Catch(OnBundleFailed)
    .Finally(CleanMemory);
```

This code would be an equivalent:

```c#
LoadBundle(path)
    .Then(OnBundleLoaded)
    .Catch(OnBundleFailed)
    .Then(CleanMemory)
    .Catch(CleanMemory);
```


## All() ##
To wait all promises to be resolved, you can use All(IEnumerable<IPromise> promises). 
Result promise will wait for all provided promises to be resolved. If any of the given promises fail, the result promise will fail.
```c#
IPromise promise = new Promise();
promise.All(new IPromise[]{
    GetAssetBundle(bundleName1),
    GetAssetBundle(bundleName2)
});


```
## Sequence() ##
To execute promises in a specific order, you can use Sequence(Func<IEnumerable<IPromise>> promises). 
Result promise will call provided Func callbacks one by one and wait for each promise returned by that callback to be resolved. When the last promise resolves, result promise gets resolved. If any of the promises fail, the resulting promise fails.
```c#
IPromise promise = new Promise();
promise.Sequence(new Func<IPromise>[]{
    GetMainBundlePromise,
    GetOtherBundlePromise,
    () => GetAssetBundlePromise(bundleName2), // If you need to pass parameters
    new Func(IPromise)
    {
        IPromise promise = new Promise();
        promise.Resolve();
        return promise;
    }
});
```

## Any() ##
Any acts similar way to All, except it resolves when one of the promises gets resolved.
```c#
IPromise promise = new Promise();
promise.Any(new IPromise[]{
    GetAssetBundle(bundleName1), // or
    GetAssetBundle(bundleName2)
});
```

## Cancellation ##
A delegate will be triggered when the user wants to cancel the promise. Implement the delegate to handle any canceling of web requests etc. Be mindful of Sequence or Then's that return new promises as the cancel will occur on those and not on the original.

A request to cancel is not guaranteed to cancel. For instance, a request to cancel an api request will not cancel if the request succeeded or failed (since the response already returned).

```c#
var promise = new Promise();
promise.CancelRequested += {
    // Determine if promise can be cancelled...
    promise.Fail(new PromiseCanceledException());
};
promise.RequestCancel();

// A canceled promise fails with PromiseCanceledException so you can Catch.
promise.Catch<PromiseCanceledException>(...);
```

The owner of the promise will fail it with the exception type `PromiseCanceledException`. This is how state is transitioned from a pending promise to a canceled, failed, promise.

See also: [Coroutine Example](#example-running-a-coroutine)


## Creating Failed and Resolved Promise ##
Promises library provides shortcut methods `Promise.Resolved()` and `Promise.Failed()` for instantly creating resolved and failed promise:

```c#
private IPromise Load(string path)
{
    if(!File.Exists(path))
    {
        return Promise.Failed(new ArgumentException("Invalid path: " + path));
    }

    if(_cachedFiles.Contains(url))
    {
        return Promise.Resolved(_cachedText[url]);
    }

    ...
}
```

Generic example:
```c#
private IPromise<string> LoadText(string url)
{
    if(string.IsNullOrEmpty(url))
    {
        return Promise<string>.Failed(new ArgumentNullException("URL can not be null or empty"));
    }

    if(_cachedText.Contains(url))
    {
        return Promise<string>.Resolved(_cachedText[url]);
    }

    ...
}
```


##  Progress Reporting ##
You can report progress using `IPromise.SetProgress(float value)` and track it using `IPromise.Progress()`:

```c#
IEnumerator LoadBundleCoro(string path, IPromise promise)
{
    var bundleLoadRequest = AssetBundle.LoadFromFileAsync(path);

    while(!bundleLoadRequest.isDone)
    {
        promise.SetProgress(bundleLoadRequest.progress);
    }

    promise.Resolve(bundleLoadRequest.assetBundle);
}

IPromise<AssetBundle> LoadBundle(string path)
{
    IPromise promise = new Promise();

    StartCoroutine(LoadBundleCoro(path, promise));

    return promise; 
}

void LoadBundle()
{
    LoadBundle("...")
        .Catch(OnError)
        .Progress(OnProgress)
        .Then(OnBundleLoaded);
}

void OnProgress(float value)
{
    progressBar.Value = value;
}
```

It is also possible to access current progress value by calling `IPromise.GetProgress()`.

## Consumer-facing Promises
Preventing consumers from calling Resolve or Fail requires that you return `IPromiseBase` instead of `IPromise`. `IPromiseBase` doesn't contain any state-changing method signatures. 

```csharp
public IPromiseBase<GameObject> MyAsyncFactory()
{
    var promise = new Promise<GameObject>();

    ...

    return promise;
}
```

## Example - Running a Coroutine
The following example demonstrates how to run wrap a coroutine into a promise object, including cancellation support and progress reporting.
Use StopCoroutine if you are using native unity coroutines instead of a [Coroutine Service](https://gitlab.aofl.com/FoundationEngineers/unity-coroutine-services/)

```c#
public IPromise SpawnEnemies(int numEnemies)
{
    IPromise promise = new Promise();

    ICoroutineHandle coroutine = _coroutineService.RunCoroutine(SpawnEnemiesOverTime(numEnemies, promise);

    // When promise is requested to cancel, if the coroutine is still running, stop the coroutine and cancel the promise
    promise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
    {
        if (promise.State == PromiseState.Pending)
        {
            _coroutineService.KillCoroutines(coroutine);
            promise.Fail(new PromiseCancelledException()); // Cancel promise
        }
    };

    return promise;
}

private IEnumerator<float> SpawnEnemiesOverTime(int numEnemies, IPromise promise)
{
    for(int numEnemy=0; numEnemy<numEnemies; numEnemy++)
    {
        SpawnEnemy(numEnemy);
    
        promise.SetProgress(numEnemy+1 / numEnemies);
        
        yield return _coroutineService.WaitForSeconds(1f);
    }
    
    promise.Resolve();
}
```

## Converting Between Promise Types
You can use `Chain()` and `Then()` variants to convert between different promise types.

To convert a **non-generic** promise into a **generic** one using a **chained** promise, use `IPromise<T> IPromise.Chain<T>(Func<IPromise<T>> callback)`:
```c#
IPromise<Texture2D> result = PlayOpenAnimation() // IPromise PlayOpenAnimation(){...} - plays open animation then resolves with no value
    .Chain(SpawnEnemiesOverTime); // IPromise<IEnemy[]> SpawnEnemiesOverTime(){...} - spawns N enemies and resolves with array of new enemies
    // "result" promise will resolve with array of enemies once animation is played and each enemy is spawned
```

To convert a **non-generic** promise into a **generic** one using a **synchronous callback**, use `IPromise<T> IPromise.Then<T>(Func<T> callback)`:
```c#
IPromise<IMyController> result = controller.PlayAnimation()
    .Then(delegate
    {
        return controller;
    });
// "result" promise is a generic promise that will resolve with a controller instance once animation is played
```

To convert a **generic** promise into a **non-generic** one using a **chained** promise, use `IPromise IPromise<T>.Chain(Func<T, Promise> callback)`:
```c#
IPromise result = LoadPrefab()
    .Chain(delegate(GameObject prefab) 
    {
        var instance = Instantiate(prefab);
        return PlayAnimation(instance); // IPromise PlayAnimation(){...} - resolves with no value when animation is done playing
    }));
// "result" promise will resolve with a prefab when prefab is loaded and animation is finished playing on that prefab
```

To convert **generic** promise with **T1** into a **generic** promise with **T2** using **chained** promise, use `IPromise<T2> IPromise<T1>.Chain<T2>(Func<T1, IPromise<T2>> callback)` method:
```c#
IPromise<GameObject> result = LoadBundle("Assets.bundle")
    .Then(delegate(AssetBundle bundle)
    {
        GameObject prefab = bundle.LoadAsset<GameObject>("name");
        return InstantiateAndPlayAnimation(prefab); // IPromise<GameObject> InstantiateAndPlayAnimation(){...} - instantiates game object and plays animation; resolves with instance of an object  
    });
// "result" promise resolves with instance of a GameObject after bundle is loaded, prefab is instantiated, and animation is done playing
```


To convert **generic** promise with **T1** into a **generic** promise with **T2** without chaining (using then callback), use `IPromise<T2> IPromise<T1>.Then<T2>(Func<T2, T1> callback)` method:
```c#
IPromise<GameObject> result = LoadBundle("Assets.bundle")
    .Then(delegate(AssetBundle bundle)
    {
        GameObject prefab = bundle.LoadAsset<GameObject>("name");
        GameObject instance = Instantiate(prefab);
        return instance;
    });
// "result" promise resolves with instance of a GameObject after bundle is loaded and prefab is instantiated
```



# Changelog #
- v1.6.0
    - Added IPromise IPromise<T>.Chain(Func<T1> callback) method that allows you to convert a generic promise into a non-generic one using a callback
    - Added IPromise<T> IPromise.Then<T>(Func<T> callback) method that allows you to convert a non-generic promise into a generic one using a synchronous callback
    - Added IPromise<T2> IPromise<T1>.Then<T2>(Func<T2, T1> callback) method that allows you to convert generic promise with T1 into a generic promise with T2 without chaining (using then callback)
    - Added missing variants to existing Chain and Then methods to allow passing additional properties, fixing inconsistency between signatures
- v1.5.4
    - Fixed bug in All() when multiple failed promises would result in "Promise has already been cancelled" exception
- v1.5.3
    - Added promise state check when promise is being requested to cancel
- v1.5.2
    - Fixed cancellation requests for Chained promises
- v1.5.1
    - Fixed bug when promise would not clear it's handlers after state change
- v1.5
    - Added IPromiseBase interface that represents consumer promise that can't be resolved or failed
- v1.4
    - Promise name is now assignable
    - Fixed NotifyTransitionStateChanged on Resolve/Fail for generic promises
- v1.3
    - Added static events for global promise state tracking
- v1.2
    - Introduced Finally() method
    - Promise cancellation now works correctly with All(), Any() and Sequence()
    - Removed ability to prevent exception handler from being Invoked when exception of that type is already caught. All exception handlers that match exception type will now get invoked in the order in which they were added.
    