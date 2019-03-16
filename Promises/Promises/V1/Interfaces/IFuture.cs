using AOFL.Promises.V1.Core;
using System;

namespace AOFL.Promises.V1.Interfaces
{
    [Obsolete("Convert to use IFuture semantic.")]
    public interface IPromiseBase : IFuture { }
    [Obsolete("Convert to use IFuture<T1> semantic.")]
    public interface IPromiseBase<T1> : IFuture<T1> { }

    public interface IFuture
    {
        PromiseState State { get; }
        string Name { get; set; }
        Exception Error { get; }

        #region Catch
        IFuture Catch(Action<Exception> callback);
        IFuture Catch<TException>(Action<TException> callback) where TException : Exception;
        #endregion

        #region Then
        IFuture Then(Action callback);
        IFuture Then<P1>(Action<P1> callback, P1 property1);
        IFuture Then<P1, P2>(Action<P1, P2> callback, P1 property1, P2 property2);
        IFuture Then<P1, P2, P3>(Action<P1, P2, P3> callback, P1 property1, P2 property2, P3 property3);

        IFuture<T> Then<T>(Func<T> callback);
        IFuture<T> Then<T, P1>(Func<P1, T> callback, P1 property1);
        IFuture<T> Then<T, P1, P2>(Func<P1, P2, T> callback, P1 property1, P2 property2);
        IFuture<T> Then<T, P1, P2, P3>(Func<P1, P2, P3, T> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Chain
        IFuture Chain(Func<IFuture> callback);
        IFuture Chain<P1>(Func<P1, IFuture> callback, P1 property1);
        IFuture Chain<P1, P2>(Func<P1, P2, IFuture> callback, P1 property1, P2 property2);
        IFuture Chain<P1, P2, P3>(Func<P1, P2, P3, IFuture> callback, P1 property1, P2 property2, P3 property3);

        IFuture<T> Chain<T>(Func<IFuture<T>> callback);
        IFuture<T> Chain<T, P1>(Func<P1, IFuture<T>> callback, P1 property1);
        IFuture<T> Chain<T, P1, P2>(Func<P1, P2, IFuture<T>> callback, P1 property1, P2 property2);
        IFuture<T> Chain<T, P1, P2, P3>(Func<P1, P2, P3, IFuture<T>> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Done
        void Done();
        void Done(Action callback);
        #endregion

        #region Progress
        IFuture Progress(Action<float> progressHandler);
        float GetProgress();
        #endregion

        #region Finally
        IFuture Finally(Action callback);
        #endregion

        void RequestCancel();
    }

    public interface IFuture<T1> : IFuture
    {
        T1 Value { get; }

        #region Then
        IFuture<T1> Then(Action<T1> callback);
        IFuture<T1> Then<P1>(Action<T1, P1> callback, P1 property1);
        IFuture<T1> Then<P1, P2>(Action<T1, P1, P2> callback, P1 property1, P2 property2);
        IFuture<T1> Then<P1, P2, P3>(Action<T1, P1, P2, P3> callback, P1 property1, P2 property2, P3 property3);

        IFuture<T2> Then<T2>(Func<T1, T2> callback);
        IFuture<T2> Then<T2, P1>(Func<T1, P1, T2> callback, P1 property1);
        IFuture<T2> Then<T2, P1, P2>(Func<T1, P1, P2, T2> callback, P1 property1, P2 property2);
        IFuture<T2> Then<T2, P1, P2, P3>(Func<T1, P1, P2, P3, T2> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Chain
        IFuture Chain(Func<T1, IFuture> callback);
        IFuture Chain<P1>(Func<T1, P1, IFuture> callback, P1 property1);
        IFuture Chain<P1, P2>(Func<T1, P1, P2, IFuture> callback, P1 property1, P2 property2);
        IFuture Chain<P1, P2, P3>(Func<T1, P1, P2, P3, IFuture> callback, P1 property1, P2 property2, P3 property3);

        IFuture<T2> Chain<T2>(Func<T1, IFuture<T2>> callback);
        IFuture<T2> Chain<T2, P1>(Func<T1, P1, IFuture<T2>> callback, P1 property1);
        IFuture<T2> Chain<T2, P1, P2>(Func<T1, P1, P2, IFuture<T2>> callback, P1 property1, P2 property2);
        IFuture<T2> Chain<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IFuture<T2>> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Catch
        new IFuture<T1> Catch(Action<Exception> callback);
        new IFuture<T1> Catch<TException>(Action<TException> callback) where TException : Exception;
        #endregion

        #region Progress
        new IFuture<T1> Progress(Action<float> progressHandler);
        #endregion

        #region Finally
        new IFuture<T1> Finally(Action callback);
        #endregion
    }
}
