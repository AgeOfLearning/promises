using System;

namespace AOFL.Promises.V1.Core.Events
{
    public enum PromiseTransistionState
    {
        Initialized,
        ResolvedBeforeCallbacks,
        ResolvedAfterCallbacks,
        FailedBeforeCallbacks,
        FailedAfterCallbacks
    }

    public class PromiseStateChangedEventArgs : EventArgs
    {
        public Promise Promise { get; set; }
        public PromiseTransistionState PromiseTransistionState { get; set; }

        public PromiseStateChangedEventArgs(Promise promise, PromiseTransistionState promiseTransistionState)
        {
            Promise = promise;
            PromiseTransistionState = promiseTransistionState;
        }
    }

    public delegate void PromiseStateChangedHandler(object sender, PromiseStateChangedEventArgs e);
}
