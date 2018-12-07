using System;

namespace AOFL.Promises.V1.Core.Events
{
    public class PromiseCancelRequestedEventArgs : EventArgs
    {
    }

    public delegate void PromiseCancelRequestedHandler(object sender, PromiseCancelRequestedEventArgs e);
}
