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
        public string Name { get; set; }
        public PromiseState State { get; protected set; }

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

        /// <summary>
        /// Represents an error that promise has failed with
        /// </summary>
        public Exception Error { get; protected set; }

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

        public event PromiseCancelRequestedHandler CancelRequested;
        
        public Promise(string name) : this()
        {
            Name = name;
        }

        public Promise()
        {
            NotifyTransitionStateChanged(PromiseTransistionState.Initialized);
        }

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
            Then(callback)
                .Done();
        }

        public IPromise Finally(Action callback)
        {
            AddFinallyHandler(callback);

            return this;
        }

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

        public IPromise Then(Action callback)
        {
            AddResolveHandler(callback);

            return this;
        }

        public IPromise Then<P1>(Action<P1> callback, P1 property1)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Finally() callback can not be null");
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
                throw new NullReferenceException("Finally() callback can not be null");
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
                throw new NullReferenceException("Finally() callback can not be null");
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
                throw new NullReferenceException("Then() callback can not be null");
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
            return Then(delegate
            {
                return callback(property1);
            });
        }

        public IPromise<T> Then<T, P1, P2>(Func<P1, P2, T> callback, P1 property1, P2 property2)
        {
            return Then(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromise<T> Then<T, P1, P2, P3>(Func<P1, P2, P3, T> callback, P1 property1, P2 property2, P3 property3)
        {
            return Then(delegate
            {
                return callback(property1, property2, property3);
            });
        }

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
                throw new NullReferenceException("Then() callback can not be null");
            }

            switch (State)
            {
                case PromiseState.Failed:
                    if (resolveCallback != null)
                    {
                        _pureResolveHandlers.Add(resolveCallback);
                    }
                    break;
                case PromiseState.Pending:
                    if (resolveCallback != null)
                    {
                        _pureResolveHandlers.Add(resolveCallback);
                    }
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
                    if (failCallback != null)
                    {
                        _catchHandlers.Add(new PromiseCatchHandler(typeof(TException), delegate (Exception e)
                        {
                            failCallback((TException)e);
                        }));
                    }
                    break;
                case PromiseState.Failed:
                    if (typeof(TException).IsAssignableFrom(Error.GetType()))
                    {
                        failCallback?.Invoke((TException)Error);
                    }
                    break;
            }
        }
        
        protected void AddFinallyHandler(Action callback)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Finally() callback can not be null");
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

        public IPromise Catch(Action<Exception> callback)
        {
            AddFailHandler(callback);

            return this;
        }

        public IPromise Catch<TException>(Action<TException> callback)
            where TException : Exception
        {
            AddFailHandler(callback);

            return this;
        }

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

        public IPromise All(IEnumerable<IPromise> promises)
        {
            IPromise returnPromise = this;

            int promiseCount = promises.Count();
            int resolved = 0;

            foreach(IPromise promise in promises)
            {
                promise.Then(delegate
                {
                    resolved++;

                    if (resolved == promiseCount && returnPromise.State == PromiseState.Pending)
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

            if(promiseCount == 0)
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

        public IPromise Then(Func<IPromise> callback)
        {
            return Chain(callback);
        }

        public IPromise<T> Then<T>(Func<IPromise<T>> callback)
        {
            return Chain<T>(callback);
        }

        public IPromise Chain(Func<IPromise> callback)
        {
            IPromise promise = new Promise();

            Action resolveHandler = delegate
            {
                IPromise chainedPromise = callback();

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

        public IPromise Chain<P1>(Func<P1, IPromise> callback, P1 property1)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Chain() callback can not be null");
            }

            return Chain(delegate
            {
                return callback(property1);
            });
        }

        public IPromise Chain<P1, P2>(Func<P1, P2, IPromise> callback, P1 property1, P2 property2)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Chain() callback can not be null");
            }

            return Chain(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromise Chain<P1, P2, P3>(Func<P1, P2, P3, IPromise> callback, P1 property1, P2 property2, P3 property3)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Chain() callback can not be null");
            }

            return Chain(delegate
            {
                return callback(property1, property2, property3);
            });
        }

        public IPromise<T> Chain<T>(Func<IPromise<T>> callback)
        {
            IPromise<T> promise = new Promise<T>();

            Action resolveHandler = delegate
            {
                IPromise<T> chainedPromise = callback();

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
        
        public IPromise<T> Chain<T, P1>(Func<P1, IPromise<T>> callback, P1 property1)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Then() callback can not be null");
            }

            return Chain(delegate
            {
                return callback(property1);
            });
        }

        public IPromise<T> Chain<T, P1, P2>(Func<P1, P2, IPromise<T>> callback, P1 property1, P2 property2)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Then() callback can not be null");
            }

            return Chain(delegate
            {
                return callback(property1, property2);
            });
        }

        public IPromise<T> Chain<T, P1, P2, P3>(Func<P1, P2, P3, IPromise<T>> callback, P1 property1, P2 property2, P3 property3)
        {
            if (callback == null)
            {
                throw new NullReferenceException("Then() callback can not be null");
            }

            return Chain(delegate
            {
                return callback(property1, property2, property3);
            });
        }

        public void RequestCancel()
        {
            if (State == PromiseState.Pending)
            {
                CancelRequested?.Invoke(this, new PromiseCancelRequestedEventArgs());
            }
        }

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
            return Chain(callback);
        }
        public IPromiseBase Chain<P1>(Func<P1, IPromiseBase> callback, P1 property1)
        {
            return Chain(callback, property1);
        }

        public IPromiseBase Chain<P1, P2>(Func<P1, P2, IPromiseBase> callback, P1 property1, P2 property2)
        {
            return Chain(callback, property1, property2);
        }

        public IPromiseBase Chain<P1, P2, P3>(Func<P1, P2, P3, IPromiseBase> callback, P1 property1, P2 property2, P3 property3)
        {
            return Chain(callback, property1, property2, property3);
        }

        public IPromiseBase<T> Chain<T>(Func<IPromiseBase<T>> callback)
        {
            return Chain(callback);
        }

        public IPromiseBase<T> Chain<T, P1>(Func<P1, IPromiseBase<T>> callback, P1 property1)
        {
            return Chain(callback, property1);
        }

        public IPromiseBase<T> Chain<T, P1, P2>(Func<P1, P2, IPromiseBase<T>> callback, P1 property1, P2 property2)
        {
            return Chain(callback, property1, property2);
        }

        public IPromiseBase<T> Chain<T, P1, P2, P3>(Func<P1, P2, P3, IPromiseBase<T>> callback, P1 property1, P2 property2, P3 property3)
        {
            return Chain(callback, property1, property2, property3);
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
        private List<Action<T1>> _resolveHandlers = new List<Action<T1>>();

        private T1 _resolvedValue;

        public T1 Value
        {
            get
            {
                return _resolvedValue;
            }
        }

        public Promise(string name) : this()
        {
            Name = name;
        }

        public Promise()
        {
            NotifyTransitionStateChanged(PromiseTransistionState.Initialized);
        }

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

        public void Done(Action<T1> callback)
        {
            Then(callback)
                .Done();
        }

        public new IPromise<T1> Finally(Action callback)
        {
            AddFinallyHandler(callback);

            return this;
        }

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
            
            ClearPromiseHandlers();

            NotifyTransitionStateChanged(PromiseTransistionState.ResolvedAfterCallbacks);
        }

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
                throw new NullReferenceException("Then() callback can not be null");
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
                throw new NullReferenceException("Then() callback can not be null");
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
                throw new NullReferenceException("Then() callback can not be null");
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
                throw new NullReferenceException("Then() callback can not be null");
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
            IPromise resultPromise = new Promise();

            Action<T1> resolveCallback = delegate (T1 value)
            {
                // Resolves and Fails through the resultPromise...
                IPromise chainedPromise = callback(value);

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

        public IPromise Chain<P1>(Func<T1, P1, IPromise> callback, P1 property1)
        {
            return Chain(delegate(T1 value) 
            {
                return callback.Invoke(value, property1);
            });
        }

        public IPromise Chain<P1, P2>(Func<T1, P1, P2, IPromise> callback, P1 property1, P2 property2)
        {
            return Chain(delegate (T1 value)
            {
                return callback.Invoke(value, property1, property2);
            });
        }

        public IPromise Chain<P1, P2, P3>(Func<T1, P1, P2, P3, IPromise> callback, P1 property1, P2 property2, P3 property3)
        {
            return Chain(delegate (T1 value)
            {
                return callback.Invoke(value, property1, property2, property3);
            });
        }

        public IPromise<T2> Chain<T2>(Func<T1, IPromise<T2>> callback)
        {
            IPromise<T2> resultPromise = new Promise<T2>();

            Action<T1> resolveCallback = delegate (T1 value)
            {
                // Resolves and Fails through resultPromise...
                IPromise<T2> chainedPromise = callback(value);

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

        public IPromise<T2> Chain<T2, P1>(Func<T1, P1, IPromise<T2>> callback, P1 property1)
        {
            IPromise<T2> resultPromise = new Promise<T2>();

            Action<T1> resolveCallback = delegate (T1 value)
            {
                // Resolves and Fails through resultPromise...
                IPromise<T2> chainedPromise = callback(value, property1);

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

        public IPromise<T2> Chain<T2, P1, P2>(Func<T1, P1, P2, IPromise<T2>> callback, P1 property1, P2 property2)
        {
            IPromise<T2> resultPromise = new Promise<T2>();

            Action<T1> resolveCallback = delegate (T1 value)
            {
                // Resolves and Fails through resultPromise...
                IPromise<T2> chainedPromise = callback(value, property1, property2);

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

        public IPromise<T2> Chain<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromise<T2>> callback, P1 property1, P2 property2, P3 property3)
        {
            IPromise<T2> resultPromise = new Promise<T2>();

            Action<T1> resolveCallback = delegate (T1 value)
            {
                // Resolves and Fails through resultPromise...
                IPromise<T2> chainedPromise = callback(value, property1, property2, property3);

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


        private void AddResolveHandler(Action<T1> resolveCallback)
        {
            switch (State)
            {
                case PromiseState.Failed:
                    if (resolveCallback != null)
                    {
                        _resolveHandlers.Add(resolveCallback);
                    }
                    break;
                case PromiseState.Pending:
                    if (resolveCallback != null)
                    {
                        _resolveHandlers.Add(resolveCallback);
                    }
                    break;
                case PromiseState.Resolved:
                    resolveCallback?.Invoke(_resolvedValue);
                    break;
            }
        }

        private void AddExceptionHandler<TException>(Action<TException> failCallback)
            where TException : Exception
        {
            switch (State)
            {
                case PromiseState.Pending:
                    if (failCallback != null)
                    {
                        _catchHandlers.Add(new PromiseCatchHandler(typeof(TException), delegate (Exception e)
                        {
                            failCallback((TException)e);
                        }));
                    }
                    break;
                case PromiseState.Failed:
                    if (typeof(TException).IsAssignableFrom(Error.GetType()))
                    {
                        failCallback?.Invoke((TException)Error);
                    }
                    break;
            }
        }
        
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
        
        public new IPromise<T1> Progress(Action<float> progressHandler)
        {
            if (_progressHandlers == null)
            {
                _progressHandlers = new List<Action<float>>();
            }

            _progressHandlers.Add(progressHandler);

            return this;
        }

        protected override void ClearPromiseHandlers()
        {
            base.ClearPromiseHandlers();
            _resolveHandlers.Clear();
        }

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
            return Chain(callback);
        }

        public IPromiseBase Chain<P1>(Func<T1, P1, IPromiseBase> callback, P1 property1)
        {
            return Chain(callback, property1);
        }

        public IPromiseBase Chain<P1, P2>(Func<T1, P1, P2, IPromiseBase> callback, P1 property1, P2 property2)
        {
            return Chain(callback, property1, property2);
        }

        public IPromiseBase Chain<P1, P2, P3>(Func<T1, P1, P2, P3, IPromiseBase> callback, P1 property1, P2 property2, P3 property3)
        {
            return Chain(callback, property1, property2, property3);
        }

        public IPromiseBase<T2> Chain<T2>(Func<T1, IPromiseBase<T2>> callback)
        {
            return Chain(callback);
        }

        public IPromiseBase<T2> Chain<T2, P1>(Func<T1, P1, IPromiseBase<T2>> callback, P1 property1)
        {
            return Chain(callback, property1);
        }

        public IPromiseBase<T2> Chain<T2, P1, P2>(Func<T1, P1, P2, IPromiseBase<T2>> callback, P1 property1, P2 property2)
        {
            return Chain(callback, property1, property2);
        }

        public IPromiseBase<T2> Chain<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromiseBase<T2>> callback, P1 property1, P2 property2, P3 property3)
        {
            return Chain(callback, property1, property2, property3);
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
