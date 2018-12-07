using System;

namespace AOFL.Promises.V1.Core.Events
{
    public class UncaughtExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        internal UncaughtExceptionEventArgs(Exception exception)
        {
            this.Exception = exception;
        }
    }

    public delegate void UncaughtExceptionHandler(object sender, UncaughtExceptionEventArgs e);
}
