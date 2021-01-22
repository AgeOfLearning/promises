using AOFL.Promises.V1.Core.Events;
using AOFL.Promises.V1.Core.Exceptions;
using AOFL.Promises.V1.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AOFL.Promises.V1.Core
{
    public class Promise : IPromise
    {
        #region Public Static Events
        public static event PromiseStateChangedHandler PromiseStateChanged
        {
            add { _promiseStateChanged += value; }
            remove { _promiseStateChanged -= value; }
        }

        public static event UncaughtExceptionHandler UncaughtExceptionThrown
        {
            add { _uncaughtExceptionThrown += value; }
            remove { _uncaughtExceptionThrown -= value; }
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// Name of the Promise
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// State of the Promise
        /// </summary>
        public PromiseState State { get; protected set; }

        /// <summary>
        /// Represents an error that promise has failed with
        /// </summary>
        public Exception Error { get; protected set; }
        #endregion

        #region Protected Fields
        /// <summary>
        /// Pure resolve handlers are called whenever promise is resolved
        /// </summary>
        protected List<Action> _pureResolveHandlers = new List<Action>();

        /// <summary>
        /// Catch callbacks are called whenever Fail() is called. KeyValuePair.Key is a type of exception.
        /// </summary>
        protected List<PromiseCatchHandler> _catchHandlers = new List<PromiseCatchHandler>();

        /// <summary>
        /// Finally handlers are added using Finally() and called whenever promise either resolves or fails
        /// </summary>
        protected List<Action> _finallyHandlers = new List<Action>();

        /// <summary>
        /// Progress handlers get called whenever promise.SetProgress is called
        /// </summary>
        protected List<Action<float>> _progressHandlers = new List<Action<float>>();

        /// <summary>
        /// Contains current progress of a promise
        /// </summary>
        protected float _progress = 0f;

        /// <summary>
        /// Uncaught exception handler will be called when Done() is called and promise doesn't have Catch(delegate(Exception){...}) handler
        /// </summary>
        protected static event UncaughtExceptionHandler _uncaughtExceptionThrown;

        /// <summary>
        /// Will be invoked on Initialized, Resolved, and Failed.
        /// </summary>
        protected static event PromiseStateChangedHandler _promiseStateChanged;
        #endregion

        #region Public Events
        public event PromiseCancelRequestedHandler CancelRequested;
        #endregion

        #region Constructors
        public Promise(string name) : this()
        {
            Name = name;
        }

        public Promise()
        {
            NotifyTransitionStateChanged(PromiseTransistionState.Initialized);
        }
        #endregion

        #region Static Methods
        public static Promise Resolved()
        {
            Promise promise = new Promise();
            promise.Resolve();

            return promise;
        }

        public static Promise Failed(Exception e)
        {
            Promise promise = new Promise();

            promise.Fail(e);

            return promise;
        }
        #endregion

        #region Fail
        public void Fail(Exception exception)
        {
            if(State == PromiseState.Failed)
            {
                throw new InvalidOperationException($"Can't fail a promise; promise has already failed with error {Error}");
            }
            else if(State == PromiseState.Resolved)
            {
                throw new InvalidOperationException($"Can't fail a promise; promise has already resolved");
            }

            Error = exception;

            State = PromiseState.Failed;

            NotifyTransitionStateChanged(PromiseTransistionState.FailedBeforeCallbacks);

            // Invoke failed handlers
            var assignableHandlers = GetAssignableFailHandlers(exception.GetType());
            
            foreach (var handler in assignableHandlers)
            {
                handler?.Invoke(exception);
            }
            
            // Invoke finally handlers
            foreach (var handler in _finallyHandlers)
            {
                handler?.Invoke();
            }
            
            ClearPromiseHandlers();

            NotifyTransitionStateChanged(PromiseTransistionState.FailedAfterCallbacks);
        }
        #endregion

        #region Done
        public void Done()
        {
            AddFailHandler(delegate(Exception exception)
            {
                var assignableHandlers = GetAssignableFailHandlers(exception.GetType());

                // If no other callbacks were added, use uncaught exception handler or throw an exeption
                if (assignableHandlers.Count() <= 1)
                {
                    if (_uncaughtExceptionThrown == null)
                    {
                        throw exception; // No _uncaughtExceptionThrown, throw this exception and hope we are in the main thread
                    }
                    else
                    {
                        _uncaughtExceptionThrown?.Invoke(this, new UncaughtExceptionEventArgs(exception)); // Otherwise, use _uncaughtExceptionThrown
                    }
                }
            });
        }

        public void Done(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Then(callback)
                .Done();
        }
        #endregion

        #region Finally
        public IPromise Finally(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            AddFinallyHandler(callback);

            return this;
        }
        #endregion

        #region Resolve
        public void Resolve()
        {
            if (State == PromiseState.Failed)
            {
                throw new InvalidOperationException($"Can't resolve a promise; promise has already failed with error {Error}");
            }
            else if (State == PromiseState.Resolved)
            {
                throw new InvalidOperationException($"Can't resolve a promise; promise has already resolved");
            }

            State = PromiseState.Resolved;

            NotifyTransitionStateChanged(PromiseTransistionState.ResolvedBeforeCallbacks);

            // Invoke pure resolve handlers
            foreach (var handler in _pureResolveHandlers)
            {
                handler?.Invoke();
            }
            
            // Invoke finally handlers
            foreach(var handler in _finallyHandlers)
            {
                handler?.Invoke();
            }

            ClearPromiseHandlers();

            NotifyTransitionStateChanged(PromiseTransistionState.ResolvedAfterCallbacks);
        }
        #endregion

        #region Then
        public IPromise Then(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            AddResolveHandler(callback);

            return this;
        }

        public IPromise Then<P1>(Action<P1> callback, P1 property1)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate
            {
                callback(property1);
            });
        }

        public IPromise Then<P1, P2>(Action<P1, P2> callback, P1 property1, P2 property2)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate
            {
                callback(property1, property2);
            });
        }

        public IPromise Then<P1, P2, P3>(Action<P1, P2, P3> callback, P1 property1, P2 property2, P3 property3)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate
            {
                callback(property1, property2, property3);
            });
        }

        public IPromise<T> Then<T>(Func<T> callback)
        {
            if(callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            IPromise<T> returnPromise = new Promise<T>();

            AddResolveHandler(delegate
            {
                returnPromise.Resolve(callback.Invoke());
            });

            AddFailHandler<Exception>(returnPromise.Fail);

            // Add request cancellation
            returnPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                if (State == PromiseState.Pending)
                {
                    RequestCancel();
                }
            };

            return returnPromise;
        }

        public IPromise<T> Then<T, P1>(Func<P1, T> callback, P1 property1)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate
            {
                return callback(property1);
            });
        }

        public IPromise<T> Then<T, P1, P2>(Func<P1, P2, T> callback, P1 property1, P2 property2)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromise<T> Then<T, P1, P2, P3>(Func<P1, P2, P3, T> callback, P1 property1, P2 property2, P3 property3)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate
            {
                return callback(property1, property2, property3);
            });
        }

        public IPromise Then(Func<IPromise> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Chain(callback);
        }

        public IPromise<T> Then<T>(Func<IPromise<T>> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Chain<T>(callback);
        }
        #endregion
        
        #region Catch
        public IPromise Catch(Action<Exception> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            AddFailHandler(callback);

            return this;
        }

        public IPromise Catch<TException>(Action<TException> callback)
            where TException : Exception
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            AddFailHandler(callback);

            return this;
        }
        #endregion

        #region Sequence
        public IPromise Sequence(IEnumerable<Func<IPromise>> promises)
        {
            List<IPromise> actingPromises = new List<IPromise>();
            int promiseCount = promises.Count();
            int index = 0;


            IPromise returnPromise = this;
            IPromise firstPromise = promises.First()?.Invoke();
            actingPromises.Add(firstPromise);

            Action resolveCallback = null;
            resolveCallback = delegate
            {
                index++;

                if (index < promiseCount)
                {
                    IPromise currentPromise = promises.ElementAt(index)?.Invoke();
                    actingPromises.Add(currentPromise);

                    currentPromise.Then(resolveCallback);
                    currentPromise.Catch(delegate(Exception e)
                    {
                        if (returnPromise.State == PromiseState.Pending)
                        {
                            returnPromise.Fail(e);
                        }
                    });
                }
                else
                {
                    returnPromise.Resolve();
                }
            };

            firstPromise.Then(resolveCallback);
            firstPromise.Catch(delegate(Exception e)
            {
                if (returnPromise.State == PromiseState.Pending)
                {
                    returnPromise.Fail(e);
                }
            });

            if (promiseCount == 0)
            {
                returnPromise.Resolve();
            }
            
            // Add request cancellation
            returnPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                IPromise currentPromise = actingPromises.ElementAt(index);
                if (currentPromise.State == PromiseState.Pending)
                {
                    currentPromise.RequestCancel();
                }
            };

            return returnPromise;
        }
        #endregion

        #region All
        public IPromise All(IEnumerable<IPromise> promises)
        {
            IPromise returnPromise = this;

            int promiseCount = 0;
            int resolved = 0;
            bool finishedIteration = false;

            foreach(IPromise promise in promises)
            {
                promiseCount++;

                promise.Then(delegate
                {
                    resolved++;

                    if (finishedIteration && resolved == promiseCount && returnPromise.State == PromiseState.Pending)
                    {
                        returnPromise.Resolve();
                    }
                });

                promise.Catch(delegate(Exception e)
                {
                    if (returnPromise.State == PromiseState.Pending)
                    {
                        returnPromise.Fail(e);
                    }
                });
            }

            finishedIteration = true;

            if (resolved == promiseCount || promiseCount == 0)
            {
                returnPromise.Resolve();
            }
            
            // Add request cancellation
            returnPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                foreach (var activePromise in promises)
                {
                    if (activePromise.State == PromiseState.Pending)
                    {
                        activePromise.RequestCancel();
                    }
                }
            };

            return returnPromise;
        }
        #endregion

        #region Any
        public IPromise Any(IEnumerable<IPromise> promises)
        {
            IPromise returnPromise = this;

            bool didResolveOrFail = false;

            foreach (IPromise promise in promises)
            {
                promise.Then(delegate
                {
                    if (!didResolveOrFail)
                    {
                        didResolveOrFail = true;
                        returnPromise.Resolve();
                    }
                });

                promise.Catch(delegate(Exception e)
                {
                    if (!didResolveOrFail)
                    {
                        didResolveOrFail = true;
                        returnPromise.Fail(e);
                    }
                });
            }

            if (promises.Count() == 0)
            {
                returnPromise.Resolve();
            }

            // Add request cancellation
            returnPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                foreach(var activePromise in promises)
                {
                    if(activePromise.State == PromiseState.Pending)
                    {
                        activePromise.RequestCancel();
                    }
                }
            };

            return returnPromise;
        }
        #endregion

        #region Chain
        public IPromise Chain(Func<IPromise> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return ChainInternal(delegate
            {
                return callback();
            });
        }

        public IPromise Chain<P1>(Func<P1, IPromise> callback, P1 property1)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return ChainInternal(delegate
            {
                return callback(property1);
            });
        }

        public IPromise Chain<P1, P2>(Func<P1, P2, IPromise> callback, P1 property1, P2 property2)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return ChainInternal(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromise Chain<P1, P2, P3>(Func<P1, P2, P3, IPromise> callback, P1 property1, P2 property2, P3 property3)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return ChainInternal(delegate
            {
                return callback(property1, property2, property3);
            });
        }

        public IPromise<T> Chain<T>(Func<IPromise<T>> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return ChainInternal(delegate
            {
                return callback();
            });
        }

        public IPromise<T> Chain<T, P1>(Func<P1, IPromise<T>> callback, P1 property1)
        {
            return ChainInternal(delegate
            {
                return callback(property1);
            });
        }

        public IPromise<T> Chain<T, P1, P2>(Func<P1, P2, IPromise<T>> callback, P1 property1, P2 property2)
        {
            return ChainInternal(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromise<T> Chain<T, P1, P2, P3>(Func<P1, P2, P3, IPromise<T>> callback, P1 property1, P2 property2, P3 property3)
        {
            return ChainInternal(delegate
            {
                return callback(property1, property2, property3);
            });
        }

        private IPromise ChainInternal(Func<IPromiseBase> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            IPromise promise = new Promise();

            Action resolveHandler = delegate
            {
                IPromiseBase chainedPromise = callback();

                chainedPromise.Catch(promise.Fail).Then((Action)promise.Resolve);

                promise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
                {
                    if (chainedPromise.State == PromiseState.Pending)
                    {
                        chainedPromise.RequestCancel();
                    }
                };
            };

            AddResolveHandler(resolveHandler);
            AddFailHandler<Exception>(promise.Fail);

            promise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                if (State == PromiseState.Pending)
                {
                    RequestCancel();
                }
            };

            return promise;
        }

        public IPromise<T> ChainInternal<T>(Func<IPromiseBase<T>> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            IPromise<T> promise = new Promise<T>();

            Action resolveHandler = delegate
            {
                IPromiseBase<T> chainedPromise = callback();

                chainedPromise.Catch(promise.Fail).Then(promise.Resolve);

                promise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
                {
                    if (chainedPromise.State == PromiseState.Pending)
                    {
                        chainedPromise.RequestCancel();
                    }
                };
            };

            AddResolveHandler(resolveHandler);
            AddFailHandler<Exception>(promise.Fail);

            promise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                if (State == PromiseState.Pending)
                {
                    RequestCancel();
                }
            };

            return promise;
        }
        #endregion

        #region RequestCancel
        public void RequestCancel()
        {
            if (State == PromiseState.Pending)
            {
                CancelRequested?.Invoke(this, new PromiseCancelRequestedEventArgs());
            }
        }
        #endregion

        #region Progress Reporting
        public IPromise Progress(Action<float> progressHandler)
        {
            if(_progressHandlers == null)
            {
                _progressHandlers = new List<Action<float>>();
            }

            _progressHandlers.Add(progressHandler);

            return this;
        }

        public void SetProgress(float value)
        {
            if(State != PromiseState.Pending)
            {
                throw new PromiseException(string.Format("Failed to set progress, invalid promise state {0}", State));
            }

            _progress = value;

            if (null != _progressHandlers)
            {
                foreach(var progressHandler in _progressHandlers)
                {
                    progressHandler?.Invoke(value);
                }
            }
        }

        public float GetProgress()
        {
            return _progress;
        }
        #endregion

        #region Private Methods
        private IEnumerable<Action<Exception>> GetAssignableFailHandlers(Type exceptionType)
        {
            return from catchHandler in _catchHandlers
                   where catchHandler.Type.IsAssignableFrom(exceptionType)
                   select catchHandler.Callback;
        }

        private void AddResolveHandler(Action resolveCallback)
        {
            if (resolveCallback == null)
            {
                throw new ArgumentNullException(nameof(resolveCallback));
            }

            switch (State)
            {
                case PromiseState.Failed:
                    _pureResolveHandlers.Add(resolveCallback);
                    break;
                case PromiseState.Pending:
                     _pureResolveHandlers.Add(resolveCallback);
                    break;
                case PromiseState.Resolved:
                    resolveCallback?.Invoke();
                    break;
            }
        }

        private void AddFailHandler<TException>(Action<TException> failCallback) where TException : Exception
        {
            switch (State)
            {
                case PromiseState.Pending:
                    if (failCallback == null)
                    {
                        throw new ArgumentNullException(nameof(failCallback));
                    }
                    _catchHandlers.Add(new PromiseCatchHandler(typeof(TException), delegate (Exception e)
                        {
                            failCallback((TException)e);
                        }));
                    break;
                case PromiseState.Failed:
                    if (typeof(TException).IsAssignableFrom(Error.GetType()))
                    {
                        failCallback?.Invoke((TException)Error);
                    }
                    break;
            }
        }
        #endregion

        #region Protected Methods
        protected void AddFinallyHandler(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            switch (State)
            {
                case PromiseState.Pending:
                    _finallyHandlers.Add(callback);
                    break;
                case PromiseState.Resolved:
                case PromiseState.Failed:
                    callback?.Invoke();
                    break;
            }
        }

        protected class PromiseCatchHandler
        {
            public Type Type;
            public Action<Exception> Callback;

            public PromiseCatchHandler(Type type, Action<Exception> callback)
            {
                Type = type;
                Callback = callback;
            }
        }

        protected void NotifyTransitionStateChanged(PromiseTransistionState promiseTransistionState)
        {
            _promiseStateChanged?.Invoke(this, new PromiseStateChangedEventArgs(this, promiseTransistionState));
        }

        protected virtual void ClearPromiseHandlers()
        {
            _pureResolveHandlers.Clear();
            _catchHandlers.Clear();
            _finallyHandlers.Clear();
            _progressHandlers.Clear();
            CancelRequested = null;
        }
        #endregion

        #region IPromiseBase Implementation

        #region Catch
        IPromiseBase IPromiseBase.Catch(Action<Exception> callback)
        {
            return Catch(callback);
        }

        IPromiseBase IPromiseBase.Catch<TException>(Action<TException> callback)
        {
            return Catch(callback);
        }
        #endregion

        #region Then
        IPromiseBase IPromiseBase.Then(Action callback)
        {
            return Then(callback);
        }


        IPromiseBase IPromiseBase.Then<P1>(Action<P1> callback, P1 property1)
        {
            return Then(callback, property1);
        }

        IPromiseBase IPromiseBase.Then<P1, P2>(Action<P1, P2> callback, P1 property1, P2 property2)
        {
            return Then(callback, property1, property2);
        }

        IPromiseBase IPromiseBase.Then<P1, P2, P3>(Action<P1, P2, P3> callback, P1 property1, P2 property2, P3 property3)
        {
            return Then(callback, property1, property2, property3);
        }

        IPromiseBase<T> IPromiseBase.Then<T>(Func<T> callback)
        {
            return Then(callback);
        }

        IPromiseBase<T> IPromiseBase.Then<T, P1>(Func<P1, T> callback, P1 property1)
        {
            return Then(callback, property1);
        }

        IPromiseBase<T> IPromiseBase.Then<T, P1, P2>(Func<P1, P2, T> callback, P1 property1, P2 property2)
        {
            return Then(callback, property1, property2);
        }

        IPromiseBase<T> IPromiseBase.Then<T, P1, P2, P3>(Func<P1, P2, P3, T> callback, P1 property1, P2 property2, P3 property3)
        {
            return Then(callback, property1, property2, property3);
        }
        #endregion

        #region Chain
        public IPromiseBase Chain(Func<IPromiseBase> callback)
        {
            return ChainInternal(callback);
        }

        public IPromiseBase Chain<P1>(Func<P1, IPromiseBase> callback, P1 property1)
        {
            return ChainInternal(delegate
            {
                return callback(property1);
            });
        }

        public IPromiseBase Chain<P1, P2>(Func<P1, P2, IPromiseBase> callback, P1 property1, P2 property2)
        {
            return ChainInternal(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromiseBase Chain<P1, P2, P3>(Func<P1, P2, P3, IPromiseBase> callback, P1 property1, P2 property2, P3 property3)
        {
            return ChainInternal(delegate
            {
                return callback(property1, property2, property3);
            });
        }

        public IPromiseBase<T> Chain<T>(Func<IPromiseBase<T>> callback)
        {
            return ChainInternal(callback);
        }

        public IPromiseBase<T> Chain<T, P1>(Func<P1, IPromiseBase<T>> callback, P1 property1)
        {
            return ChainInternal(delegate
            {
                return callback(property1);
            });
        }

        public IPromiseBase<T> Chain<T, P1, P2>(Func<P1, P2, IPromiseBase<T>> callback, P1 property1, P2 property2)
        {
            return ChainInternal(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromiseBase<T> Chain<T, P1, P2, P3>(Func<P1, P2, P3, IPromiseBase<T>> callback, P1 property1, P2 property2, P3 property3)
        {
            return ChainInternal(delegate
            {
                return callback(property1, property2, property3);
            });
        }
        #endregion

        #region Progress
        IPromiseBase IPromiseBase.Progress(Action<float> progressHandler)
        {
            return Progress(progressHandler);
        }
        #endregion

        #region Finally
        IPromiseBase IPromiseBase.Finally(Action callback)
        {
            return Finally(callback);
        }
        #endregion
        #endregion
    }

    public class Promise<T1> : Promise, IPromise<T1>, IDisposable
    {
        #region Public Properties
        public T1 Value
        {
            get
            {
                return _resolvedValue;
            }
        }
        #endregion

        #region Private Fields
        private List<Action<T1>> _resolveHandlers = new List<Action<T1>>();

        private T1 _resolvedValue;
        #endregion

        #region Constructors
        public Promise(string name) : this()
        {
            Name = name;
        }

        public Promise()
        {
            NotifyTransitionStateChanged(PromiseTransistionState.Initialized);
        }
        #endregion

        #region Static Methods
        public static Promise<T1> Resolved(T1 value)
        {
            Promise<T1> promise = new Promise<T1>();

            promise.Resolve(value);

            return promise;
        }
        
        public static new Promise<T1> Failed(Exception e)
        {
            Promise<T1> promise = new Promise<T1>();

            promise.Fail(e);

            return promise;
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _resolvedValue = default(T1);
            }
        }
        #endregion

        #region Done
        public void Done(Action<T1> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Then(callback)
                .Done();
        }
        #endregion

        #region Finally
        public new IPromise<T1> Finally(Action callback)
        {
            AddFinallyHandler(callback);

            return this;
        }
        #endregion

        #region Resolve
        [Obsolete("Invalid use of Resolve() on a generic Promise<>. Use Promise.Resolve<T>(T value).", true)]
        public new void Resolve()
        {
            Resolve(default(T1));
        }

        public void Resolve(T1 value)
        {
            if (State == PromiseState.Failed)
            {
                throw new InvalidOperationException($"Can't resolve a promise; promise has already failed with error {Error}");
            }
            else if (State == PromiseState.Resolved)
            {
                throw new InvalidOperationException($"Can't resolve a promise; promise has already resolved with value {Value}");
            }

            State = PromiseState.Resolved;
            _resolvedValue = value;

            NotifyTransitionStateChanged(PromiseTransistionState.ResolvedBeforeCallbacks);

            for (int i = 0; i < _resolveHandlers.Count; i++)
            {
                _resolveHandlers[i](_resolvedValue);
            }
            
            // Resolve pure callbacks
            for (int i = 0; i < _pureResolveHandlers.Count; i++)
            {
                _pureResolveHandlers[i]();
            }
            
            // Invoke finally handlers
            foreach(var handler in _finallyHandlers)
            {
                handler?.Invoke();
            }
            
            ClearPromiseHandlers();

            NotifyTransitionStateChanged(PromiseTransistionState.ResolvedAfterCallbacks);
        }
        #endregion

        #region Then
        public IPromise<T1> Then(Action<T1> callback)
        {
            AddResolveHandler(callback);

            return this;
        }

        public IPromise<T1> Then<P1>(Action<T1, P1> callback, P1 property1)
        {
            Action<T1> resolveCallback = delegate (T1 value)
            {
                callback(value, property1);
            };

            AddResolveHandler(resolveCallback);

            return this;
        }

        public IPromise<T1> Then<P1, P2>(Action<T1, P1, P2> callback, P1 property1, P2 property2)
        {
            Action<T1> resolveCallback = delegate (T1 value)
            {
                callback(value, property1, property2);
            };

            AddResolveHandler(resolveCallback);

            return this;
        }

        public IPromise<T1> Then<P1, P2, P3>(Action<T1, P1, P2, P3> callback, P1 property1, P2 property2, P3 property3)
        {
            Action<T1> resolveCallback = delegate (T1 value)
            {
                callback(value, property1, property2, property3);
            };

            AddResolveHandler(resolveCallback);

            return this;
        }

        public IPromise<T2> Then<T2>(Func<T1, T2> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            IPromise<T2> returnPromise = new Promise<T2>();

            AddResolveHandler(delegate(T1 value)
            {
                returnPromise.Resolve(callback.Invoke(value));
            });

            AddExceptionHandler<Exception>(returnPromise.Fail);

            // Add request cancellation
            returnPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                if (State == PromiseState.Pending)
                {
                    RequestCancel();
                }
            };

            return returnPromise;
        }

        public IPromise<T2> Then<T2, P1>(Func<T1, P1, T2> callback, P1 property1)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate(T1 value) 
            {
                return callback(value, property1);
            });
        }

        public IPromise<T2> Then<T2, P1, P2>(Func<T1, P1, P2, T2> callback, P1 property1, P2 property2)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate (T1 value)
            {
                return callback(value, property1, property2);
            });
        }

        public IPromise<T2> Then<T2, P1, P2, P3>(Func<T1, P1, P2, P3, T2> callback, P1 property1, P2 property2, P3 property3)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return Then(delegate (T1 value)
            {
                return callback(value, property1, property2, property3);
            });
        }

        public IPromise<T2> Then<T2>(Func<T1, IPromise<T2>> callback)
        {
            return Chain<T2>(callback);
        }

        public IPromise<T2> Then<T2, P1>(Func<T1, P1, IPromise<T2>> callback, P1 property1)
        {
            return Chain<T2, P1>(callback, property1);
        }

        public IPromise<T2> Then<T2, P1, P2>(Func<T1, P1, P2, IPromise<T2>> callback, P1 property1, P2 property2)
        {
            return Chain<T2, P1, P2>(callback, property1, property2);
        }

        public IPromise<T2> Then<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromise<T2>> callback, P1 property1, P2 property2, P3 property3)
        {
            return Chain<T2, P1, P2, P3>(callback, property1, property2, property3);
        }
        #endregion

        #region Chain
        public IPromise Chain(Func<T1, IPromise> callback)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value);
            });
        }

        public IPromise Chain<P1>(Func<T1, P1, IPromise> callback, P1 property1)
        {
            return ChainInternal(delegate(T1 value) 
            {
                return callback(value, property1);
            });
        }

        public IPromise Chain<P1, P2>(Func<T1, P1, P2, IPromise> callback, P1 property1, P2 property2)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2);
            });
        }

        public IPromise Chain<P1, P2, P3>(Func<T1, P1, P2, P3, IPromise> callback, P1 property1, P2 property2, P3 property3)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2, property3);
            });
        }

        public IPromise<T2> Chain<T2>(Func<T1, IPromise<T2>> callback)
        {
            return ChainInternal(delegate(T1 value) 
            {
                return callback(value);
            });
        }

        public IPromise<T2> Chain<T2, P1>(Func<T1, P1, IPromise<T2>> callback, P1 property1)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1);
            });
        }

        public IPromise<T2> Chain<T2, P1, P2>(Func<T1, P1, P2, IPromise<T2>> callback, P1 property1, P2 property2)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2);
            });
        }

        public IPromise<T2> Chain<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromise<T2>> callback, P1 property1, P2 property2, P3 property3)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2, property3);
            });
        }

        public IPromise ChainInternal(Func<T1, IPromiseBase> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            IPromise resultPromise = new Promise();

            Action<T1> resolveCallback = delegate (T1 value)
            {
                // Resolves and Fails through the resultPromise...
                IPromiseBase chainedPromise = callback(value);

                chainedPromise.Catch(resultPromise.Fail).Then(resultPromise.Resolve);

                resultPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
                {
                    if (chainedPromise.State == PromiseState.Pending)
                    {
                        chainedPromise.RequestCancel();
                    }
                };
            };

            AddResolveHandler(resolveCallback);
            AddExceptionHandler<Exception>(resultPromise.Fail);

            resultPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                if (State == PromiseState.Pending)
                {
                    RequestCancel();
                }
            };

            return resultPromise;
        }
        
        public IPromise<T2> ChainInternal<T2>(Func<T1, IPromiseBase<T2>> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            IPromise<T2> resultPromise = new Promise<T2>();

            Action<T1> resolveCallback = delegate (T1 value)
            {
                // Resolves and Fails through resultPromise...
                IPromiseBase<T2> chainedPromise = callback(value);

                chainedPromise.Catch(resultPromise.Fail).Then(resultPromise.Resolve);

                resultPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
                {
                    if (chainedPromise.State == PromiseState.Pending)
                    {
                        chainedPromise.RequestCancel();
                    }
                };
            };

            AddResolveHandler(resolveCallback);
            AddExceptionHandler<Exception>(resultPromise.Fail);

            resultPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                if (State == PromiseState.Pending)
                {
                    RequestCancel();
                }
            };

            return resultPromise;
        }
        #endregion

        #region Private Methods
        private void AddResolveHandler(Action<T1> resolveCallback)
        {
            if (resolveCallback == null)
            {
                throw new ArgumentNullException(nameof(resolveCallback));
            }

            switch (State)
            {
                case PromiseState.Failed:
                    _resolveHandlers.Add(resolveCallback);
                    break;
                case PromiseState.Pending:
                    _resolveHandlers.Add(resolveCallback);
                    break;
                case PromiseState.Resolved:
                    resolveCallback?.Invoke(_resolvedValue);
                    break;
            }
        }

        private void AddExceptionHandler<TException>(Action<TException> failCallback)
            where TException : Exception
        {
            if (failCallback == null)
            {
                throw new ArgumentNullException(nameof(failCallback));
            }

            switch (State)
            {
                case PromiseState.Pending:
                    _catchHandlers.Add(new PromiseCatchHandler(typeof(TException), delegate (Exception e)
                    {
                        failCallback((TException)e);
                    }));
                    break;
                case PromiseState.Failed:
                    if (typeof(TException).IsAssignableFrom(Error.GetType()))
                    {
                        failCallback?.Invoke((TException)Error);
                    }
                    break;
            }
        }
        #endregion

        #region Catch
        public new IPromise<T1> Catch(Action<Exception> callback)
        {
            AddExceptionHandler(callback);

            return this;
        }

        public new IPromise<T1> Catch<TException>(Action<TException> callback)
            where TException : Exception
        {
            AddExceptionHandler(callback);

            return this;
        }
        #endregion

        #region Sequence
        public new IPromise<IEnumerable<IPromise>> Sequence(IEnumerable<Func<IPromise>> promises)
        {
            IPromise<IEnumerable<IPromise>> returnPromise;
            try
            {
                returnPromise = (IPromise<IEnumerable<IPromise>>)this;
            }
            catch(InvalidCastException e)
            {
                throw new PromiseException("You can only use Sequence(IEnumerable<Func<IPromise>> promises) with IPromise<IEnumerable<IPromise>>", e);
            }

            List<IPromise> actingPromises = new List<IPromise>();
            int promiseCount = promises.Count();
            int index = 0;

            IPromise firstPromise = promises.First()?.Invoke();
            actingPromises.Add(firstPromise);
            
            Action resolveCallback = null;
            resolveCallback = delegate
            {
                index++;

                if (index < promiseCount)
                {
                    IPromise currentPromise = promises.ElementAt(index)?.Invoke();
                    actingPromises.Add(currentPromise);

                    currentPromise.Then(resolveCallback);
                    currentPromise.Catch(delegate(Exception e)
                    {
                        if (returnPromise.State == PromiseState.Pending)
                        {
                            returnPromise.Fail(e);
                        }
                    });
                }
                else
                {
                    returnPromise.Resolve(actingPromises);
                }
            };

            firstPromise.Then(resolveCallback);
            firstPromise.Catch(delegate(Exception e)
            {
                if (returnPromise.State == PromiseState.Pending)
                {
                    returnPromise.Fail(e);
                }
            });

            // Add request cancellation
            returnPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                IPromise currentPromise = actingPromises.ElementAt(index);
                if (currentPromise.State == PromiseState.Pending)
                {
                    currentPromise.RequestCancel();
                }
            };

            return returnPromise;
        }

        public IPromise<T1> Sequence(T1 initialValue, IEnumerable<Func<T1, IPromise<T1>>> promises)
        {
            IPromise<T1> returnPromise = this;

            int promiseCount = promises.Count();

            int index = 0;

            IPromise<T1> firstPromise = promises.First()?.Invoke(initialValue);

            IPromise<T1> currentPromise = null;

            Action<T1> resolveCallback = null;
            resolveCallback = delegate(T1 value)
            {
                index++;

                if (index < promiseCount)
                {
                    currentPromise = promises.ElementAt(index)?.Invoke(value);
                    currentPromise.Then(resolveCallback);
                    currentPromise.Catch(delegate (Exception e)
                    {
                        if (returnPromise.State == PromiseState.Pending)
                        {
                            returnPromise.Fail(e);
                        }
                    });
                }
                else
                {
                    returnPromise.Resolve(value);
                }
            };

            firstPromise.Then(resolveCallback);
            firstPromise.Catch(delegate (Exception e)
            {
                if (returnPromise.State == PromiseState.Pending)
                {
                    returnPromise.Fail(e);
                }
            });

            // Add request cancellation
            returnPromise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                if (currentPromise.State == PromiseState.Pending)
                {
                    currentPromise.RequestCancel();
                }
            };

            return returnPromise;
        }
        #endregion

        #region Aggregate
        public IPromise<T1> Aggregate(IEnumerable<T1> source, Func<T1, T1, IPromise<T1>> func)
        {
            return Sequence(source.First(), Enumerable.Select<T1, Func<T1, IPromise<T1>>>(source.Skip(1), a => b => func(a, b)));
        }

        public IPromise<T1> Aggregate(IEnumerable<T1> source, T1 initialValue, Func<T1, T1, IPromise<T1>> func)
        {
            return Sequence(initialValue, Enumerable.Select<T1, Func<T1, IPromise<T1>>>(source, a => b => func(a, b)));
        }

        public IPromise<T1> Aggregate(IEnumerable<T1> source, T1 initialValue, Func<T1, T1, IPromise<T1>> func, Func<T1, IPromise<T1>> resultSelector)
        {
            return Sequence(initialValue, Enumerable.Select<T1, Func<T1, IPromise<T1>>>(source, a => b => func(a, b)))
                .Chain(resultSelector);
        }
        #endregion

        #region Progress
        public new IPromise<T1> Progress(Action<float> progressHandler)
        {
            if (_progressHandlers == null)
            {
                _progressHandlers = new List<Action<float>>();
            }

            _progressHandlers.Add(progressHandler);

            return this;
        }
        #endregion

        #region Protected Methods
        protected override void ClearPromiseHandlers()
        {
            base.ClearPromiseHandlers();
            _resolveHandlers.Clear();
        }
        #endregion

        #region IPromiseBase Implementation
        #region Then
        IPromiseBase<T1> IPromiseBase<T1>.Then(Action<T1> callback)
        {
            return Then(callback);
        }

        IPromiseBase<T1> IPromiseBase<T1>.Then<P1>(Action<T1, P1> callback, P1 property1)
        {
            return Then(callback, property1);
        }

        IPromiseBase<T1> IPromiseBase<T1>.Then<P1, P2>(Action<T1, P1, P2> callback, P1 property1, P2 property2)
        {
            return Then(callback, property1, property2);
        }

        IPromiseBase<T1> IPromiseBase<T1>.Then<P1, P2, P3>(Action<T1, P1, P2, P3> callback, P1 property1, P2 property2, P3 property3)
        {
            return Then(callback, property1, property2, property3);
        }

        IPromiseBase<T2> IPromiseBase<T1>.Then<T2>(Func<T1, T2> callback)
        {
            return Then(callback);
        }

        IPromiseBase<T2> IPromiseBase<T1>.Then<T2, P1>(Func<T1, P1, T2> callback, P1 property1)
        {
            return Then(callback, property1);
        }

        IPromiseBase<T2> IPromiseBase<T1>.Then<T2, P1, P2>(Func<T1, P1, P2, T2> callback, P1 property1, P2 property2)
        {
            return Then(callback, property1, property2);
        }

        IPromiseBase<T2> IPromiseBase<T1>.Then<T2, P1, P2, P3>(Func<T1, P1, P2, P3, T2> callback, P1 property1, P2 property2, P3 property3)
        {
            return Then(callback, property1, property2, property3);
        }
        #endregion

        #region Chain
        public IPromiseBase Chain(Func<T1, IPromiseBase> callback)
        {
            return ChainInternal(callback);
        }

        public IPromiseBase Chain<P1>(Func<T1, P1, IPromiseBase> callback, P1 property1)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1);
            });
        }

        public IPromiseBase Chain<P1, P2>(Func<T1, P1, P2, IPromiseBase> callback, P1 property1, P2 property2)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2);
            });
        }

        public IPromiseBase Chain<P1, P2, P3>(Func<T1, P1, P2, P3, IPromiseBase> callback, P1 property1, P2 property2, P3 property3)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2, property3);
            });
        }

        public IPromiseBase<T2> Chain<T2>(Func<T1, IPromiseBase<T2>> callback)
        {
            return ChainInternal(callback);
        }

        public IPromiseBase<T2> Chain<T2, P1>(Func<T1, P1, IPromiseBase<T2>> callback, P1 property1)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1);
            });
        }

        public IPromiseBase<T2> Chain<T2, P1, P2>(Func<T1, P1, P2, IPromiseBase<T2>> callback, P1 property1, P2 property2)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2);
            });
        }

        public IPromiseBase<T2> Chain<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromiseBase<T2>> callback, P1 property1, P2 property2, P3 property3)
        {
            return ChainInternal(delegate (T1 value)
            {
                return callback(value, property1, property2, property3);
            });
        }
        #endregion

        #region Catch
        IPromiseBase<T1> IPromiseBase<T1>.Catch(Action<Exception> callback)
        {
            return Catch(callback);
        }

        IPromiseBase<T1> IPromiseBase<T1>.Catch<TException>(Action<TException> callback)
        {
            return Catch(callback);
        }
        #endregion

        #region Progress
        IPromiseBase<T1> IPromiseBase<T1>.Progress(Action<float> progressHandler)
        {
            return Progress(progressHandler);
        }
        #endregion

        #region Finally
        IPromiseBase<T1> IPromiseBase<T1>.Finally(Action callback)
        {
            return Finally(callback);
        }
        #endregion
        #endregion
    }
}
