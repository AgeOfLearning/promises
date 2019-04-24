using AOFL.Promises.V1.Core;
using AOFL.Promises.V1.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AOFL.Promises.V1.Interfaces
{
    public interface IPromise : IPromiseBase
    {
        event PromiseCancelRequestedHandler CancelRequested;

        new PromiseState State { get; }
        new string Name { get; set; }
        new Exception Error { get; }

        IPromise Sequence(IEnumerable<Func<IPromise>> promises);
        IPromise All(IEnumerable<IPromise> promises);
        IPromise Any(IEnumerable<IPromise> promises);

        #region Catch
        new IPromise Catch(Action<Exception> callback);
        new IPromise Catch<TException>(Action<TException> callback) where TException : Exception;
        #endregion

        #region Then
        new IPromise Then(Action callback);
        new IPromise Then<P1>(Action<P1> callback, P1 property1);
        new IPromise Then<P1, P2>(Action<P1, P2> callback, P1 property1, P2 property2);
        new IPromise Then<P1, P2, P3>(Action<P1, P2, P3> callback, P1 property1, P2 property2, P3 property3);

        new IPromise<T> Then<T>(Func<T> callback);
        new IPromise<T> Then<T, P1>(Func<P1, T> callback, P1 property1);
        new IPromise<T> Then<T, P1, P2>(Func<P1, P2, T> callback, P1 property1, P2 property2);
        new IPromise<T> Then<T, P1, P2, P3>(Func<P1, P2, P3, T> callback, P1 property1, P2 property2, P3 property3);

        [Obsolete("Use Chain(IPromise<T>)")]
        IPromise<T> Then<T>(Func<IPromise<T>> callback);
        #endregion

        #region Chain
        IPromise Chain(Func<IPromise> callback);
        IPromise Chain<P1>(Func<P1, IPromise> callback, P1 property1);
        IPromise Chain<P1, P2>(Func<P1, P2, IPromise> callback, P1 property1, P2 property2);
        IPromise Chain<P1, P2, P3>(Func<P1, P2, P3, IPromise> callback, P1 property1, P2 property2, P3 property3);

        IPromise<T> Chain<T>(Func<IPromise<T>> callback);
        IPromise<T> Chain<T, P1>(Func<P1, IPromise<T>> callback, P1 property1);
        IPromise<T> Chain<T, P1, P2>(Func<P1, P2, IPromise<T>> callback, P1 property1, P2 property2);
        IPromise<T> Chain<T, P1, P2, P3>(Func<P1, P2, P3, IPromise<T>> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Progress
        new IPromise Progress(Action<float> progressHandler);
        void SetProgress(float value);
        new float GetProgress();
        #endregion

        #region Finally
        new IPromise Finally(Action callback);
        #endregion

        #region Done
        new void Done();
        new void Done(Action callback);
        #endregion

        void Resolve();
        void Fail(Exception exception);

        new void RequestCancel();
    }

    public interface IPromise<T1> : IPromiseBase<T1>, IPromise
    {
        new T1 Value { get; }

        #region Sequence
        new IPromise<IEnumerable<IPromise>> Sequence(IEnumerable<Func<IPromise>> promises);
        IPromise<T1> Sequence(T1 initialValue, IEnumerable<Func<T1, IPromise<T1>>> promises);
        #endregion

        #region All
        new IPromise All(IEnumerable<IPromise> promises);
        #endregion

        #region Aggregate
        IPromise<T1> Aggregate(IEnumerable<T1> source, Func<T1, T1, IPromise<T1>> func);
        IPromise<T1> Aggregate(IEnumerable<T1> source, T1 initialValue, Func<T1, T1, IPromise<T1>> func);
        IPromise<T1> Aggregate(IEnumerable<T1> source, T1 initialValue, Func<T1, T1, IPromise<T1>> func, Func<T1, IPromise<T1>> resultSelector);
        #endregion

        #region Then
        new IPromise<T1> Then(Action<T1> callback);
        new IPromise<T1> Then<P1>(Action<T1, P1> callback, P1 property1);
        new IPromise<T1> Then<P1, P2>(Action<T1, P1, P2> callback, P1 property1, P2 property2);
        new IPromise<T1> Then<P1, P2, P3>(Action<T1, P1, P2, P3> callback, P1 property1, P2 property2, P3 property3);

        new IPromise<T2> Then<T2>(Func<T1, T2> callback);
        new IPromise<T2> Then<T2, P1>(Func<T1, P1, T2> callback, P1 property1);
        new IPromise<T2> Then<T2, P1, P2>(Func<T1, P1, P2, T2> callback, P1 property1, P2 property2);
        new IPromise<T2> Then<T2, P1, P2, P3>(Func<T1, P1, P2, P3, T2> callback, P1 property1, P2 property2, P3 property3);


        [Obsolete("Use Chain")]
        IPromise<T2> Then<T2>(Func<T1, IPromise<T2>> callback);
        [Obsolete("Use Chain")]
        IPromise<T2> Then<T2, P1>(Func<T1, P1, IPromise<T2>> callback, P1 property1);
        [Obsolete("Use Chain")]
        IPromise<T2> Then<T2, P1, P2>(Func<T1, P1, P2, IPromise<T2>> callback, P1 property1, P2 property2);
        [Obsolete("Use Chain")]
        IPromise<T2> Then<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromise<T2>> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Chain
        IPromise Chain(Func<T1, IPromise> callback);
        IPromise Chain<P1>(Func<T1, P1, IPromise> callback, P1 property1);
        IPromise Chain<P1, P2>(Func<T1, P1, P2, IPromise> callback, P1 property1, P2 property2);
        IPromise Chain<P1, P2, P3>(Func<T1, P1, P2, P3, IPromise> callback, P1 property1, P2 property2, P3 property3);

        IPromise<T2> Chain<T2>(Func<T1, IPromise<T2>> callback);
        IPromise<T2> Chain<T2, P1>(Func<T1, P1, IPromise<T2>> callback, P1 property1);
        IPromise<T2> Chain<T2, P1, P2>(Func<T1, P1, P2, IPromise<T2>> callback, P1 property1, P2 property2);
        IPromise<T2> Chain<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromise<T2>> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Catch
        new IPromise<T1> Catch(Action<Exception> callback);
        new IPromise<T1> Catch<TException>(Action<TException> callback) where TException : Exception;
        #endregion

        #region Progress
        new IPromise<T1> Progress(Action<float> progressHandler);
        #endregion

        #region Finally
        new IPromise<T1> Finally(Action callback);
        #endregion

        [Obsolete("Invalid use of Resolve() on a generic Promise<>. Use Promise.Resolve<T>(T value).", true)]
        new void Resolve();
        void Resolve(T1 value);
        void Done(Action<T1> callback);
    }
}
