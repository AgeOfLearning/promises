using AOFL.Promises.V1.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AOFL.Promises.V1.Interfaces
{
    public interface IPromiseBase
    {
        PromiseState State { get; }
        string Name { get; set; }
        Exception Error { get; }

        #region Catch
        IPromiseBase Catch(Action<Exception> callback);
        IPromiseBase Catch<TException>(Action<TException> callback) where TException : Exception;
        #endregion

        #region Then
        IPromiseBase Then(Action callback);
        IPromiseBase Then<P1>(Action<P1> callback, P1 property1);
        IPromiseBase Then<P1, P2>(Action<P1, P2> callback, P1 property1, P2 property2);
        IPromiseBase Then<P1, P2, P3>(Action<P1, P2, P3> callback, P1 property1, P2 property2, P3 property3);

        IPromiseBase<T> Then<T>(Func<T> callback);
        IPromiseBase<T> Then<T, P1>(Func<P1, T> callback, P1 property1);
        IPromiseBase<T> Then<T, P1, P2>(Func<P1, P2, T> callback, P1 property1, P2 property2);
        IPromiseBase<T> Then<T, P1, P2, P3>(Func<P1, P2, P3, T> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Chain
        IPromiseBase Chain(Func<IPromiseBase> callback);
        IPromiseBase Chain<P1>(Func<P1, IPromiseBase> callback, P1 property1);
        IPromiseBase Chain<P1, P2>(Func<P1, P2, IPromiseBase> callback, P1 property1, P2 property2);
        IPromiseBase Chain<P1, P2, P3>(Func<P1, P2, P3, IPromiseBase> callback, P1 property1, P2 property2, P3 property3);

        IPromiseBase<T> Chain<T>(Func<IPromiseBase<T>> callback);
        IPromiseBase<T> Chain<T, P1>(Func<P1, IPromiseBase<T>> callback, P1 property1);
        IPromiseBase<T> Chain<T, P1, P2>(Func<P1, P2, IPromiseBase<T>> callback, P1 property1, P2 property2);
        IPromiseBase<T> Chain<T, P1, P2, P3>(Func<P1, P2, P3, IPromiseBase<T>> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Done
        void Done();
        void Done(Action callback);
        #endregion

        #region Progress
        IPromiseBase Progress(Action<float> progressHandler);
        float GetProgress();
        #endregion

        #region Finally
        IPromiseBase Finally(Action callback);
        #endregion

        void RequestCancel();
    }

    public interface IPromiseBase<T1> : IPromiseBase
    {
        T1 Value { get; }

        #region Then
        IPromiseBase<T1> Then(Action<T1> callback);
        IPromiseBase<T1> Then<P1>(Action<T1, P1> callback, P1 property1);
        IPromiseBase<T1> Then<P1, P2>(Action<T1, P1, P2> callback, P1 property1, P2 property2);
        IPromiseBase<T1> Then<P1, P2, P3>(Action<T1, P1, P2, P3> callback, P1 property1, P2 property2, P3 property3);

        IPromiseBase<T2> Then<T2>(Func<T1, T2> callback);
        IPromiseBase<T2> Then<T2, P1>(Func<T1, P1, T2> callback, P1 property1);
        IPromiseBase<T2> Then<T2, P1, P2>(Func<T1, P1, P2, T2> callback, P1 property1, P2 property2);
        IPromiseBase<T2> Then<T2, P1, P2, P3>(Func<T1, P1, P2, P3, T2> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Chain
        IPromiseBase Chain(Func<T1, IPromiseBase> callback);
        IPromiseBase Chain<P1>(Func<T1, P1, IPromiseBase> callback, P1 property1);
        IPromiseBase Chain<P1, P2>(Func<T1, P1, P2, IPromiseBase> callback, P1 property1, P2 property2);
        IPromiseBase Chain<P1, P2, P3>(Func<T1, P1, P2, P3, IPromiseBase> callback, P1 property1, P2 property2, P3 property3);

        IPromiseBase<T2> Chain<T2>(Func<T1, IPromiseBase<T2>> callback);
        IPromiseBase<T2> Chain<T2, P1>(Func<T1, P1, IPromiseBase<T2>> callback, P1 property1);
        IPromiseBase<T2> Chain<T2, P1, P2>(Func<T1, P1, P2, IPromiseBase<T2>> callback, P1 property1, P2 property2);
        IPromiseBase<T2> Chain<T2, P1, P2, P3>(Func<T1, P1, P2, P3, IPromiseBase<T2>> callback, P1 property1, P2 property2, P3 property3);
        #endregion

        #region Catch
        new IPromiseBase<T1> Catch(Action<Exception> callback);
        new IPromiseBase<T1> Catch<TException>(Action<TException> callback) where TException : Exception;
        #endregion

        #region Progress
        new IPromiseBase<T1> Progress(Action<float> progressHandler);
        #endregion

        #region Finally
        new IPromiseBase<T1> Finally(Action callback);
        #endregion
    }
}
