using System;

namespace AOFL.Promises.V1.Core.Exceptions
{
    public class PromiseException : Exception
    {
        public PromiseException()
        {
        }

        public PromiseException(string message)
            : base(message)
        {
        }

        public PromiseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
