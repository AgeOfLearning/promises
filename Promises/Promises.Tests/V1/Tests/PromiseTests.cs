using AOFL.Promises.V1.Core;
using AOFL.Promises.V1.Core.Events;
using AOFL.Promises.V1.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AOFL.Promises.Tests.V1.Tests
{
    [TestClass]
    public class PromiseTests
    {
        #region Promise
        #region Promise.All
        [TestMethod]
        public void Promise_All_DoesReturnSelf()
        {
            IPromise promise = new Promise();
            IPromise promise2 = promise.All(new IPromise[] { GetBoolResolvedPromise() });
            Assert.AreEqual(promise, promise2);
        }

        [TestMethod]
        public void Promise_All_DoesNotThrow_WhenMultiplePromisesAreFailed()
        {
            bool didCatch = false;

            IPromise promise = new Promise().All(new IPromise[]
            {
                GetFailedPromise(),
                GetFailedPromise()
            })
            .Catch((e) => didCatch = true); // Does not throw "promise is already failed" exception

            Assert.IsTrue(didCatch, "Did not catch");
            Assert.AreEqual(PromiseState.Failed, promise.State, "Did not fail");
        }
        #endregion

        #region Promise.Any
        [TestMethod]
        public void Promise_Any_DoesReturnSelf()
        {
            IPromise promise = new Promise();
            IPromise promise2 = promise.Any(new IPromise[] { GetBoolResolvedPromise() });
            Assert.AreEqual(promise, promise2);
        }

        [TestMethod]
        public void Promise_Any_InvokesCancelRequestedOnPendingPromises_WhenAnyIsUsed_WhenMultiplePromisesFail()
        {
            int numCancelled = 0;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                numCancelled++;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                numCancelled++;
            };

            IPromise any = new Promise().Any(new IPromise[] { promise1, promise2 });
            any.RequestCancel();

            Assert.AreEqual(2, numCancelled, "did not invoke CancelRequested on all promises");
        }
        #endregion

        #region Promise.Catch
        [TestMethod]
        public void Promise_Catch_DoesNotInvokeCallbackTwice_WhenBothSequencedPromisesFailed()
        {
            int numFailed = 0;

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { GetBoolFailedPromise, GetBoolFailedPromise })
                .Catch(delegate
                {
                    numFailed++;
                });

            Assert.AreEqual(1, numFailed, "Failed more than once");
        }

        [TestMethod]
        public void Promise_Catch_DoesNotInvokeMultipleTimes_WhenAnyPromisesAreFailed()
        {
            int numFailed = 0;

            IPromise promise = new Promise();
            promise.Any(new IPromise[] { GetBoolFailedPromise(), GetBoolFailedPromise() })
                .Catch(delegate
                {
                    numFailed++;
                });

            Assert.AreEqual(1, numFailed, "Failed multiple times");
        }

        [TestMethod]
        public void Promise_Catch_InvoeksCallback_WhenParallelPromiseCatches()
        {
            bool didCatch = false;
            bool allDidCatch = false;
            bool allDidResolve = false;

            Exception argumentException = new ArgumentException();

            Action<ArgumentException> onCatch = delegate (ArgumentException e)
            {
                didCatch = true;
                Assert.AreEqual(e, argumentException);
            };

            IPromise promise = new Promise();
            promise.All(new IPromise[]
            {
                GetFailedCatchingPromise(argumentException, onCatch),
                GetResolvedPromise()
            })
            .Then(delegate ()
            {
                allDidResolve = true;
            })
            .Catch(delegate (Exception e)
            {
                Assert.AreEqual(e, argumentException);

                allDidCatch = true;
            });

            Assert.AreEqual(didCatch, true);
            Assert.AreEqual(allDidResolve, false);
            Assert.AreEqual(allDidCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_Invokes_WhenMultipleCallbacks()
        {
            Exception e = new Exception("Failed Successfully!");
            int eCount = 0;

            GetFailedPromise(e)
                .Catch((exception) => { eCount++; })
                .Catch((exception) => { eCount++; });

            Assert.AreEqual(eCount, 2);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenAllIsUsed_WhenOnePromiseFails()
        {
            bool didFail = false;

            IPromise promise = new Promise();
            promise.All(new IPromise[] { GetFailedPromise(), GetResolvedPromise() })
                .Catch(delegate
                {
                    didFail = true;
                });

            Assert.IsTrue(didFail, "resulting promise did not fail");
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenAnyIsUsed_WhenPromiseIsFailed()
        {
            bool didFail = false;

            IPromise promise = new Promise();
            promise.Any(new IPromise[] { GetBoolFailedPromise() })
                .Catch(delegate
                {
                    didFail = true;
                });

            Assert.IsTrue(didFail, "Did not fail");
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenChainedPromiseFails()
        {
            bool didCatch = false;

            IPromise promise = GetBoolResolvedPromise()
                .Chain(GetBoolFailedPromise)
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenChainedPromiseFails_WithLambda()
        {
            bool didCatch = false;

            IPromise promise = GetBoolResolvedPromise()
                .Chain(() => GetFailedPromise(new Exception()))
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenFirstSequencedMethodFailed()
        {
            bool didCatch = false;

            IPromise promise = new Promise();
            promise.Sequence(new Func<IPromise>[]
            {
                () => GetBoolFailedPromise(),
                () => GetBoolResolvedPromise()
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }


        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenSecondSequencedMethodFailed()
        {
            bool didCatch = false;

            IPromise promise = new Promise();
            promise.Sequence(new Func<IPromise>[]
            {
                () => GetResolvedPromise(),
                () => GetFailedPromise(new Exception())
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenThirdSequencedMethodFailed()
        {
            bool didCatch = false;

            IPromise promise = new Promise();
            promise.Sequence(new Func<IPromise>[]
            {
                () => GetResolvedPromise(),
                () => GetResolvedPromise(),
                () => GetFailedPromise(new Exception())
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenGenericChainedPromiseFails_WithLambda()
        {
            bool didCatch = false;

            IPromise promise = GetResolvedPromise()
                .Chain(() => GetBoolFailedPromise(new Exception()))
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenPromiseIsFailed()
        {
            bool didCatch = false;

            IPromise promise = new Promise();
            promise.Fail(new Exception());

            promise.Catch(delegate
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_CastToIPromiseBase_Catch_InvokesCallback_WhenPromiseIsFailed()
        {
            bool didCatch = false;

            IPromise promise = new Promise();
            promise.Fail(new Exception());

            var testPromise = (IPromiseBase)promise;

            testPromise.Catch(delegate
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenPromiseIsResolved()
        {
            Exception e = new Exception("Failed Successfully!");

            GetBoolFailedPromise(e).Catch(delegate (Exception exception)
            {
                Assert.AreEqual(e, exception);
            });
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenSequencedPromiseCatches()
        {
            bool didCatch = false;
            bool sequenceDidCatch = false;
            bool sequenceDidResolve = false;

            Action<Exception> onCatch = delegate (Exception e)
            {
                didCatch = true;
            };

            IPromise promise = new Promise();
            promise.Sequence(new Func<IPromise>[]
            {
                () => GetFailedCatchingPromise(new Exception(), onCatch),
                () => GetResolvedPromise()
            })
            .Then(delegate ()
            {
                sequenceDidResolve = true;
            })
            .Catch(delegate (Exception e)
            {
                sequenceDidCatch = true;
            });

            Assert.AreEqual(didCatch, true);
            Assert.AreEqual(sequenceDidResolve, false);
            Assert.AreEqual(sequenceDidCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WhenSequencedPromiseIsFailed()
        {
            bool didCatch = false;

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { GetBoolResolvedPromise, GetBoolFailedPromise, GetIntResolvedPromise })
                .Catch(delegate
                {
                    didCatch = true;
                });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Catch_InvokesCallback_WithChainedPromise_WhenFirstPromiseFailed()
        {
            bool didCatch = false;

            GetBoolFailedPromise()
                .Chain(GetBoolResolvedPromise)
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            Assert.AreEqual(didCatch, true);
        }
        #endregion

        #region Promise.Chain
        #region Promise.Chain
        [TestMethod]
        public void Promise_Chain_InvokesNextPromise_WhenFirstPromiseResolved()
        {
            bool didPropagate = false;

            var promise = GetPromise();

            promise.Chain(() =>
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                return secondPromise;
            });

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
        }

        [TestMethod]
        public void Promise_Chain_InvokesNextPromise_WhenFirstPromiseResolved_WithOneProperty()
        {
            bool didPropagate = false;
            string p1 = null;

            var promise = GetPromise();

            promise.Chain(delegate (string property1)
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                p1 = property1;
                return secondPromise;
            }, "abc");

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("abc", p1, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Chain_InvokesNextPromise_WhenFirstPromiseResolved_WithTwoProperties()
        {
            bool didPropagate = false;
            string p1 = null;
            string p2 = null;

            var promise = GetPromise();

            promise.Chain(delegate (string property1, string property2)
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                p1 = property1;
                p2 = property2;
                return secondPromise;
            }, "abc", "def");

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Chain_InvokesNextPromise_WhenFirstPromiseResolved_WithThreeProperties()
        {
            bool didPropagate = false;
            string p1 = null;
            string p2 = null;
            string p3 = null;

            var promise = GetPromise();

            promise.Chain(delegate (string property1, string property2, string property3)
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                p1 = property1;
                p2 = property2;
                p3 = property3;
                return secondPromise;
            }, "abc", "def", "ghi");

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
            Assert.AreEqual("ghi", p3, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Chain_PropagatesCatchHandler()
        {
            bool didCatch = false;
            var promise = GetBoolPromise();
            promise.Chain(GetBoolPromise)
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            promise.Fail(new Exception());

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Chain_PropagatesCancellationHandler()
        {
            bool requestedToCancel = false;

            var firstPromise = GetPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancel = true;
            };

            var resultPromise = firstPromise.Chain(GetPromise);
            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancel, true, "did not request to cancel");
        }

        [TestMethod]
        public void Promise_Chain_PropagatesCancellationHandler_WhenFirstPromiseResolved()
        {
            bool requestedToCancelFirstPromise = false;
            bool requestedToCancelSecondPromise = false;

            var firstPromise = GetPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancelFirstPromise = true;
            };

            var resultPromise = firstPromise.Chain(() =>
            {
                var secondPromise = GetPromise();
                secondPromise.CancelRequested += (sender, e) =>
                {
                    requestedToCancelSecondPromise = true;
                };
                return secondPromise;
            });

            firstPromise.Resolve();

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancelFirstPromise, false, "requested to cancel first promise when it shouldn't have");
            Assert.AreEqual(requestedToCancelSecondPromise, true, "did not request to cancel second promise");
        }
        #endregion

        #region Promise.Chain<T>
        [TestMethod]
        public void Promise_GenericChain_InvokesNextPromise_WhenFirstPromiseResolved()
        {
            bool didPropagate = false;

            var promise = GetPromise();

            promise.Chain(() =>
            {
                var secondPromise = GetBoolResolvedPromise();
                didPropagate = true;
                return secondPromise;
            });

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
        }

        [TestMethod]
        public void Promise_GenericChain_InvokesNextPromise_WhenFirstPromiseResolved_WithOneProperty()
        {
            bool didPropagate = false;
            string p1 = null;

            var promise = GetPromise();

            promise.Chain(delegate (string property1)
            {
                var secondPromise = GetBoolResolvedPromise();
                didPropagate = true;
                p1 = property1;
                return secondPromise;
            }, "abc");

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("abc", p1, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_GenericChain_InvokesNextPromise_WhenFirstPromiseResolved_WithTwoProperties()
        {
            bool didPropagate = false;
            string p1 = null;
            string p2 = null;

            var promise = GetPromise();

            promise.Chain(delegate (string property1, string property2)
            {
                var secondPromise = GetBoolResolvedPromise();
                didPropagate = true;
                p1 = property1;
                p2 = property2;
                return secondPromise;
            }, "abc", "def");

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_GenericChain_InvokesNextPromise_WhenFirstPromiseResolved_WithThreeProperties()
        {
            bool didPropagate = false;
            string p1 = null;
            string p2 = null;
            string p3 = null;

            var promise = GetPromise();

            promise.Chain(delegate (string property1, string property2, string property3)
            {
                var secondPromise = GetBoolResolvedPromise();
                didPropagate = true;
                p1 = property1;
                p2 = property2;
                p3 = property3;
                return secondPromise;
            }, "abc", "def", "ghi");

            promise.Resolve();

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
            Assert.AreEqual("ghi", p3, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_GenericChain_PropagatesCatchHandler()
        {
            bool didCatch = false;
            var promise = GetPromise();
            promise.Chain(GetBoolPromise)
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            promise.Fail(new Exception());

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_GenericChain_PropagatesCancellationHandler()
        {
            bool requestedToCancel = false;

            var firstPromise = GetPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancel = true;
            };

            var resultPromise = firstPromise.Chain(GetPromise);
            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancel, true, "did not request to cancel");
        }

        [TestMethod]
        public void Promise_GenericChain_PropagatesCancellationHandler_WhenFirstPromiseResolved()
        {
            bool requestedToCancelFirstPromise = false;
            bool requestedToCancelSecondPromise = false;

            var firstPromise = GetPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancelFirstPromise = true;
            };

            var resultPromise = firstPromise.Chain(() =>
            {
                var secondPromise = GetPromise();
                secondPromise.CancelRequested += (sender, e) =>
                {
                    requestedToCancelSecondPromise = true;
                };
                return secondPromise;
            });

            firstPromise.Resolve();

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancelFirstPromise, false, "requested to cancel first promise when it shouldn't have");
            Assert.AreEqual(requestedToCancelSecondPromise, true, "did not request to cancel second promise");
        }
        #endregion

        #endregion

        #region Promise.Done
        [TestMethod]
        public void Promise_Done_InvokesCallback_WhenPromiseIsResolved()
        {
            bool didResolve = false;
            GetBoolResolvedPromise()
                .Done(delegate
                {
                    didResolve = true;
                });

            Assert.AreEqual(didResolve, true);
        }
        #endregion

        #region Promise.Error
        [TestMethod]
        public void Promise_Error_ContainsException_WhenPromiseIsFailed()
        {
            Exception e = new Exception();

            IPromise promise = new Promise();
            promise.Fail(e);

            Assert.AreEqual(e, promise.Error, "error is not valid");
        }
        #endregion

        #region Promise.Fail
        [TestMethod]
        public void Promise_Fail_InvokesCatchCallback()
        {
            bool didInvokeCatch = false;

            IPromise promise = GetPromise();
            promise.Catch(delegate (Exception e)
            {
                didInvokeCatch = true;
            });

            promise.Fail(new Exception());

            Assert.IsTrue(didInvokeCatch, "Did not invoke catch callback");
        }

        [TestMethod]
        public void Promise_Fail_InvokesCatchCallback_WhenGenericThenIsUsed()
        {
            bool didInvokeCatch = false;

            IPromise promise = GetPromise();
            IPromise<int> second = promise.Then(delegate
            {
                return 456;
            });
            second.Catch(delegate (Exception e)
            {
                didInvokeCatch = true;
            });

            promise.Fail(new Exception());

            Assert.IsTrue(didInvokeCatch, "Did not propagate catch callback");
        }

        [TestMethod]
        public void Promise_Fail_InvokesCatchCallback_WhenGenericPromiseGenericThenIsUsed()
        {
            bool didInvokeCatch = false;

            IPromise<int> promise = new Promise<int>();
            IPromise<string> second = promise.Then(delegate (int value)
            {
                return "value=" + value.ToString();
            });
            second.Catch(delegate (Exception e)
            {
                didInvokeCatch = true;
            });

            promise.Fail(new Exception());

            Assert.IsTrue(didInvokeCatch, "Did not propagate catch callback");
        }

        [TestMethod]
        public void Promise_Fail_DoesNotHideException_WhenUncaughtExceptionHandlerIsNotSet_WithDone()
        {
            bool didCatch = false;
            bool promiseDidCatch = false;

            IPromise promise = new Promise();

            promise.Catch(delegate (ArgumentException e)
            {
                promiseDidCatch = true;
            })
            .Done();

            try
            {
                promise.Fail(new Exception());
            }
            catch (Exception)
            {
                didCatch = true;
            }

            Assert.AreEqual(promiseDidCatch, false);
            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Fail_DoesNotInvoke_WhenExceptionIsDifferentType()
        {
            bool didCatchArgumentException = false;
            bool didCatchSystemException = false;

            IPromise promise = new Promise();
            promise.Catch(delegate (ArgumentException e)
            {
                didCatchArgumentException = true;
            });
            promise.Catch(delegate (Exception e)
            {
                didCatchSystemException = true;
            });

            promise.Fail(new InvalidOperationException());

            Assert.IsFalse(didCatchArgumentException, "did catch ArgumentException");
            Assert.IsTrue(didCatchSystemException, "did not catch System.Exception");
        }

        [TestMethod]
        public void Promise_Fail_DoesNotInvokeCatchCallback_WhenExceptionTypeDoesNotMatch()
        {
            bool exceptionCaught = false;

            IPromise promise = new Promise();
            promise.Catch(delegate (ArgumentException e)
            {
                exceptionCaught = true;
            });

            promise.Fail(new Exception());

            Assert.AreEqual(exceptionCaught, false);
        }

        [TestMethod]
        public void Promise_Fail_HidesException_WhenDoneIsNotCalled()
        {
            bool didCatch = false;
            bool promiseDidCatch = false;

            IPromise promise = new Promise();

            promise.Catch(delegate (ArgumentException e)
            {
                promiseDidCatch = true;
            });

            try
            {
                promise.Fail(new Exception());
            }
            catch (Exception)
            {
                didCatch = true;
            }

            Assert.AreEqual(promiseDidCatch, false);
            Assert.AreEqual(didCatch, false);
        }

        [TestMethod]
        public void Promise_Fail_InvokesCatchCallback_WithCustomException()
        {
            bool exceptionCaught = false;

            IPromise promise = new Promise();
            promise.Catch(delegate (ArgumentException e)
            {
                exceptionCaught = true;
            });

            promise.Fail(new ArgumentException());

            Assert.AreEqual(exceptionCaught, true);
        }

        [TestMethod]
        public void Promise_Fail_InvokesCatchCallbacks_WhenMultipleCallbacks()
        {
            Exception e = new Exception("Failed Successfully!");
            int eCount = 0;

            IPromise promise = new Promise();

            promise.Catch((exception) => { eCount++; })
                .Catch((exception) => { eCount++; });

            promise.Fail(new Exception());

            Assert.AreEqual(eCount, 2);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Promise_Fail_ThrowsInvalidOperationException_WhenPromiseIsAlreadyFailed()
        {
            IPromise promise = new Promise();
            promise.Fail(new Exception("First exception"));
            promise.Fail(new Exception("Second exception"));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Promise_Fail_ThrowsInvalidOperationException_WhenPromiseIsAlreadyResolved()
        {
            IPromise promise = new Promise();
            promise.Resolve();
            promise.Fail(new Exception("Second exception"));
        }

        [TestMethod]
        public void Promise_Fail_ClearsResolveHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Then(() =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Fail(new Exception());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Fail_ClearsCatchHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Catch((Exception e) =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Fail(new Exception());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Fail_ClearsFinallyHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Finally(() =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Fail(new Exception());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Fail_ClearsProgressHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Progress((progress) =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Fail(new Exception());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Fail_ClearsCancelRequestedHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.CancelRequested += (sender, e) =>
                {
                    Console.WriteLine(obj);
                };
            })();

            promise.Fail(new Exception());

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Fail_DoesNotThrowException_WhenAllIsUsed_WhenMultiplePromisesFail()
        {
            bool didCatch = false;

            IPromise promise1 = GetPromise();
            IPromise promise2 = GetPromise();
            IPromise allPromise = new Promise().All(new IPromise[] { promise1, promise2 })
                .Catch(e => didCatch = true);

            promise1.Fail(new Exception());
            promise2.Fail(new Exception()); // Does not throw "promise already cancelled exception"

            Assert.IsTrue(didCatch, "Did not catch");
            Assert.AreEqual(PromiseState.Failed, allPromise.State, "Result promise did not fail properly");
        }

        [TestMethod]
        public void Promise_Fail_DoesNotThrowException_WhenAllIsUsed_WhenResultPromiseIsManuallyFailed()
        {
            bool didCatch = false;

            IPromise promise1 = GetPromise();
            IPromise promise2 = GetPromise();
            IPromise allPromise = new Promise().All(new IPromise[] { promise1, promise2 })
                .Catch(e => didCatch = true);

            allPromise.Fail(new Exception());

            promise1.Fail(new Exception()); // Does not throw "promise already cancelled exception"

            Assert.IsTrue(didCatch, "Did not catch");
            Assert.AreEqual(PromiseState.Failed, allPromise.State, "Result promise did not fail properlt");
        }

        [TestMethod]
        public void Promise_Fail_DoesNotThrowException_WhenSequenceIsUsed_WhenResultPromiseIsManuallyFailed()
        {
            bool didCatch = false;

            IPromise promise1 = GetPromise();
            IPromise promise2 = GetPromise();
            IPromise sequencePromise = new Promise().Sequence(new Func<IPromise>[] { () => promise1, () => promise2 })
                .Catch(e => didCatch = true);

            sequencePromise.Fail(new Exception());

            promise1.Fail(new Exception()); // Does not throw "promise already cancelled exception"

            Assert.IsTrue(didCatch, "Did not catch");
            Assert.AreEqual(PromiseState.Failed, sequencePromise.State, "Result promise did not fail properlt");
        }
        #endregion

        #region Promise.Failed
        [TestMethod]
        public void Promise_Failed_ReturnsFailedPromise()
        {
            Exception e = new Exception("some error");
            IPromise promise = Promise.Failed(e);

            Assert.AreEqual(PromiseState.Failed, promise.State, "promise did not fail properly");

            bool didCatch = false;

            promise.Catch(delegate (Exception exc)
            {
                didCatch = true;

                Assert.AreEqual(e, exc, "promise exception is invalid");
            });

            Assert.IsTrue(didCatch, "promise did not catch");
        }
        #endregion

        #region Promise.Finally
        [TestMethod]
        public void Promise_Finally_DoesNotHideException_WhenPromiseFailed()
        {
            bool didCatch = false;

            GetFailedPromise(new Exception())
                .Finally(() => { })
                .Catch<Exception>(delegate (Exception e)
                {
                    didCatch = true;
                });

            Assert.IsTrue(didCatch, "did not catch");
        }

        [TestMethod]
        public void Promise_Finally_DoesNotHideException_WhenThrown()
        {
            bool defaultExceptionHandlerCaught = false;

            UncaughtExceptionHandler handler = delegate (object sender, UncaughtExceptionEventArgs e)
            {
                defaultExceptionHandlerCaught = true;
            };

            Promise.UncaughtExceptionThrown += handler;

            try
            {
                GetFailedPromise(new Exception())
                    .Finally(() => { })
                    .Done();
            }
            finally
            {
                Promise.UncaughtExceptionThrown -= handler;
            }

            Assert.IsTrue(defaultExceptionHandlerCaught, "Did not invoke uncaught exception handler");
        }

        [TestMethod]
        public void Promise_Finally_InvokesCallback_WhenPromiseIsFailed()
        {
            bool didInvoke = false;

            GetFailedPromise(new Exception())
                .Finally(() =>
                {
                    didInvoke = true;
                });

            Assert.IsTrue(didInvoke, "did not invoke Finally()");
        }

        [TestMethod]
        public void Promise_Finally_InvokesCallback_WhenPromiseIsResolved()
        {
            bool didInvoke = false;

            GetResolvedPromise()
                .Finally(() =>
                {
                    didInvoke = true;
                });

            Assert.IsTrue(didInvoke, "did not invoke Finally()");
        }

        [TestMethod]
        public void Promise_Finally_InvokesCallback_WhenSequenceIsFailed()
        {
            bool didCatch = false;
            bool didInvokeFinally = false;

            IPromise sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { GetFailedPromise })
                .Catch(delegate
                {
                    didCatch = true;
                })
                .Finally(delegate
                {
                    didInvokeFinally = true;
                });

            Assert.IsTrue(didCatch, "did not catch failed promise");
            Assert.IsTrue(didInvokeFinally, "did not invoke Finally");
        }

        [TestMethod]
        public void Promise_Finally_ReturnsPromise()
        {
            IPromise promise = GetPromise()
                .Finally(() => { });

            Assert.AreNotEqual(null, promise, "returned value is null");
        }
        #endregion

        #region Promise.GetProgress
        [TestMethod]
        public void Promise_GetProgress_ReturnsPromiseProgress()
        {
            IPromise<string> promise = new Promise<string>();

            promise.SetProgress(0.4f);

            Assert.AreEqual(0.4f, promise.GetProgress(), "promise.GetProgress did not return correct progress");
        }
        #endregion

        #region Promise.RequestCancel
        [TestMethod]
        public void Promise_RequestCancel_DoesNotInvokeCallback_WhenAnyIsUsed_WhenPromiseIsFailed()
        {
            bool didCancel = false;

            IPromise promise = GetPromise();
            promise.CancelRequested += delegate
            {
                didCancel = true;
            };

            promise.Fail(new Exception());

            IPromise any = new Promise().Any(new IPromise[] { promise });
            any.RequestCancel();

            Assert.IsFalse(didCancel, "did try to cancel resolved promise");
        }

        [TestMethod]
        public void Promise_RequestCancel_DoesNotInvokeCallback_WhenAllIsUsed_WhenPromiseIsResolved()
        {
            int numCancelled = 0;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                numCancelled++;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                numCancelled++;
            };

            promise1.Resolve();

            IPromise promise = new Promise();
            promise = promise.All(new IPromise[] { promise1, promise2 });

            promise.RequestCancel();

            Assert.AreEqual(1, numCancelled, "All() did not pass RequestCancel");
        }

        [TestMethod]
        public void Promise_RequestCancel_DoesNotInvokeCallbackMultipleTimes_WhenAnyIsUsed()
        {
            bool didCancel = false;

            IPromise promise = GetPromise();
            promise.CancelRequested += delegate
            {
                didCancel = true;
            };

            promise.Resolve();

            IPromise any = new Promise().Any(new IPromise[] { promise });
            any.RequestCancel();

            Assert.IsFalse(didCancel, "did try to cancel resolved promise");
        }

        [TestMethod]
        public void Promise_RequestCancel_InvokesAllCallbacks_WhenAllIsUsed()
        {
            int numCancelled = 0;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                numCancelled++;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                numCancelled++;
            };

            IPromise promise = new Promise();
            promise = promise.All(new IPromise[] { promise1, promise2 });

            promise.RequestCancel();

            Assert.AreEqual(2, numCancelled, "All() did not pass RequestCancel");
        }

        [TestMethod]
        public void Promise_RequestCancel_InvokesCallback()
        {
            bool didInvoke = false;

            IPromise promise = new Promise();
            promise.CancelRequested += delegate
            {
                didInvoke = true;
            };

            promise.RequestCancel();

            Assert.IsTrue(didInvoke, "did not invoke CancelRequested");
        }

        [TestMethod]
        public void Promise_RequestCancel_InvokesCallback_WhenGenericThenIsUsed()
        {
            bool didInvoke = false;

            IPromise promise = new Promise();
            IPromise<int> second = promise.Then(delegate
            {
                return 567;
            });

            promise.CancelRequested += delegate
            {
                didInvoke = true;
            };

            second.RequestCancel();

            Assert.IsTrue(didInvoke, "did not propagate cancellation callback");
        }

        [TestMethod]
        public void Promise_RequestCancel_DoesNotInvokeCallback_WhenPromiseIsResolved()
        {
            bool didInvoke = false;

            var promise = GetResolvedPromise();
            promise.CancelRequested += delegate
            {
                didInvoke = true;
            };

            promise.RequestCancel();

            Assert.IsFalse(didInvoke, "did invoke CancelRequested");
        }

        [TestMethod]
        public void Promise_RequestCancel_DoesNotInvokeCallback_WhenPromiseIsFailed()
        {
            bool didInvoke = false;

            var promise = GetFailedPromise();
            promise.CancelRequested += delegate
            {
                didInvoke = true;
            };

            promise.RequestCancel();

            Assert.IsFalse(didInvoke, "did invoke CancelRequested");
        }

        [TestMethod]
        public void Promise_RequestCancel_InvokesCallback_OnActiveSequencedPromise()
        {
            bool firstCancelled = false, secondCancelled = false;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                firstCancelled = true;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                secondCancelled = true;
            };

            IPromise sequence = new Promise().Sequence(new Func<IPromise>[] { () => { return promise1; }, () => { return promise2; } });
            sequence.RequestCancel();

            Assert.IsTrue(firstCancelled, "did not request cancel on a first promise");
            Assert.IsFalse(secondCancelled, "did request cancel on a second promise");
        }

        [TestMethod]
        public void Promise_RequestCancel_InvokesCallback_OnActiveSequencedPromise_WhenFirstPromiseIsResolved()
        {
            bool firstCancelled = false, secondCancelled = false;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                firstCancelled = true;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                secondCancelled = true;
            };

            promise1.Resolve(); // Resolve first promise, second promise is active in a sequence.

            IPromise sequence = new Promise().Sequence(new Func<IPromise>[] { () => { return promise1; }, () => { return promise2; } });
            sequence.RequestCancel();

            Assert.IsFalse(firstCancelled, "did request cancel on a first promise");
            Assert.IsTrue(secondCancelled, "did not request cancel on a second promise");
        }
        #endregion

        #region Promise.Resolve
        [TestMethod]
        public void Promise_Resolve_InvokesThenCallback()
        {
            bool didResolve = false;

            IPromise promise = GetPromise();
            promise.Then(delegate
            {
                didResolve = true;
            });

            promise.Resolve();

            Assert.IsTrue(didResolve, "did not invoke");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesThenCallback_WithOneProperty()
        {
            bool didResolve = false;
            string p1 = null;

            IPromise promise = GetPromise();
            promise.Then(delegate (string property1)
            {
                didResolve = true;
                p1 = property1;
            }, "abc");

            promise.Resolve();

            Assert.IsTrue(didResolve, "did not invoke");
            Assert.AreEqual("abc", p1, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesThenCallback_WithTwoProperties()
        {
            bool didResolve = false;
            string p1 = null;
            string p2 = null;

            IPromise promise = GetPromise();
            promise.Then(delegate (string property1, string property2)
            {
                didResolve = true;
                p1 = property1;
                p2 = property2;
            }, "abc", "def");

            promise.Resolve();

            Assert.IsTrue(didResolve, "did not invoke");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesThenCallback_WithThreeProperties()
        {
            bool didResolve = false;
            string p1 = null;
            string p2 = null;
            string p3 = null;

            IPromise promise = GetPromise();
            promise.Then(delegate (string property1, string property2, string property3)
            {
                didResolve = true;
                p1 = property1;
                p2 = property2;
                p3 = property3;
            }, "abc", "def", "hgi");

            promise.Resolve();

            Assert.IsTrue(didResolve, "did not invoke");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
            Assert.AreEqual("hgi", p3, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesDoneCallback()
        {
            bool didResolve = false;

            IPromise promise = new Promise();
            promise.Done(delegate
            {
                didResolve = true;
            });

            promise.Resolve();

            Assert.AreEqual(didResolve, true);
        }

        [TestMethod]
        public void Promise_Resolve_InvokesPureAndGenericThenCallbacks()
        {
            int result = 0;

            IPromise<int> promise = new Promise<int>();
            promise.Then(delegate (int value)
            {
                result += value;
            });

            promise.Then(delegate ()
            {
                result += 1;
            });

            promise.Resolve(2);

            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericThenCallback()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise promise = new Promise();
            IPromise<string> second = promise.Then(delegate ()
            {
                return "abc";
            });

            second.Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve();

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("abc", resolvedValue, "Did not resolve");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericThenCallback_WithOneProperty()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise promise = new Promise();
            IPromise<string> second = promise.Then(delegate (string property1)
            {
                return $"{property1}";
            }, "abc");

            second.Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve();

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("abc", resolvedValue, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericThenCallback_WithTwoProperties()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise promise = new Promise();
            IPromise<string> second = promise.Then(delegate (string property1, string property2)
            {
                return $"{property1}, {property2}";
            }, "abc", "def");

            second.Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve();

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("abc, def", resolvedValue, "Did not propagate properties");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericThenCallback_WithThreeProperties()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise promise = new Promise();
            IPromise<string> second = promise.Then(delegate (string property1, string property2, string property3)
            {
                return $"{property1}, {property2}, {property3}";
            }, "abc", "def", "ghi");

            second.Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve();

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("abc, def, ghi", resolvedValue, "Did not propagate properties");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesChainCallback()
        {
            bool didInvoke = false;

            IPromise promise = new Promise();
            promise.Chain(delegate {
                didInvoke = true;
                return Promise.Resolved();
            });

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesChainCallback_WithOneProperty()
        {
            bool didInvoke = false;
            string p1 = null;

            IPromise promise = new Promise();
            promise.Chain(delegate (string property1) {
                didInvoke = true;
                p1 = property1;
                return Promise.Resolved();
            }, "abc");

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
            Assert.AreEqual("abc", p1, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesChainCallback_WithTwoProperties()
        {
            bool didInvoke = false;
            string p1 = null;
            string p2 = null;

            IPromise promise = new Promise();
            promise.Chain(delegate (string property1, string property2) {
                didInvoke = true;
                p1 = property1;
                p2 = property2;
                return Promise.Resolved();
            }, "abc", "def");

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesChainCallback_WithThreeProperties()
        {
            bool didInvoke = false;
            string p1 = null;
            string p2 = null;
            string p3 = null;

            IPromise promise = new Promise();
            promise.Chain(delegate (string property1, string property2, string property3) {
                didInvoke = true;
                p1 = property1;
                p2 = property2;
                p3 = property3;
                return Promise.Resolved();
            }, "abc", "def", "ghi");

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
            Assert.AreEqual("ghi", p3, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericChainCallback()
        {
            bool didInvoke = false;

            IPromise promise = new Promise();
            promise.Chain(delegate {
                didInvoke = true;
                return Promise<int>.Resolved(123);
            });

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericChainCallback_WithOneProperty()
        {
            bool didInvoke = false;
            string p1 = null;

            IPromise promise = new Promise();
            promise.Chain(delegate (string property1) {
                didInvoke = true;
                p1 = property1;
                return Promise<int>.Resolved(123);
            }, "abc");

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
            Assert.AreEqual("abc", p1, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericChainCallback_WithTwoProperties()
        {
            bool didInvoke = false;
            string p1 = null;
            string p2 = null;

            IPromise promise = new Promise();
            promise.Chain(delegate (string property1, string property2) {
                didInvoke = true;
                p1 = property1;
                p2 = property2;
                return Promise<int>.Resolved(123);
            }, "abc", "def");

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Resolve_InvokesGenericChainCallback_WithThreeProperties()
        {
            bool didInvoke = false;
            string p1 = null;
            string p2 = null;
            string p3 = null;

            IPromise promise = new Promise();
            promise.Chain(delegate (string property1, string property2, string property3) {
                didInvoke = true;
                p1 = property1;
                p2 = property2;
                p3 = property3;
                return Promise<int>.Resolved(123);
            }, "abc", "def", "ghi");

            promise.Resolve();

            Assert.IsTrue(didInvoke, "Did not invoke chained callback");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
            Assert.AreEqual("ghi", p3, "Did not propagate property");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Promise_Resolve_ThrowsInvalidOperationException_WhenPromiseIsAlreadyFailed()
        {
            IPromise promise = new Promise();
            promise.Fail(new Exception("Exception"));
            promise.Resolve();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Promise_Resolve_ThrowsInvalidOperationException_WhenPromiseIsAlreadyResolved()
        {
            IPromise promise = new Promise();
            promise.Resolve();
            promise.Resolve();
        }

        [TestMethod]
        public void Promise_Resolve_ClearsResolveHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Then(() =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Resolve_ClearsCatchHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Catch((exception) =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Resolve_ClearsFinallyHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Finally(() =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Resolve_ClearsProgressHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Progress((progress) =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void Promise_Resolve_ClearsCancelRequestedHandlers()
        {
            WeakReference weakReference = null;

            IPromise promise = new Promise();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.CancelRequested += (sender, e) =>
                {
                    Console.WriteLine(obj);
                };
            })();

            promise.Resolve();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }
        #endregion

        #region Promise.Resolved
        [TestMethod]
        public void Promise_Resolved_ReturnsResolvedPromise()
        {
            IPromise promise = Promise.Resolved();
            Assert.AreEqual(PromiseState.Resolved, promise.State, "promise is not resolved");

            bool didResolve = false;
            promise.Then(() =>
            {
                didResolve = true;
            });

            Assert.IsTrue(didResolve, "promise did not resolve");
        }
        #endregion

        #region Promise.Sequence
        [TestMethod]
        public void Promise_Sequence_DoesNotInvokeFurtherCallbacks_WhenPromiseFails()
        {
            Func<IPromise> SecondCallback = delegate ()
            {
                Assert.Fail("Sequence did not stop - invoked second method");
                return GetResolvedPromise();
            };

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { GetBoolFailedPromise, SecondCallback });
        }

        [TestMethod]
        public void Promise_Sequence_DoesReturnSelf()
        {
            IPromise promise = new Promise();
            IPromise promise2 = promise.Sequence(new Func<IPromise>[] { GetBoolResolvedPromise });
            Assert.AreEqual(promise, promise2);
        }
        #endregion

        #region Promise.SetProgress
        [TestMethod]
        public void Promise_SetProgress_InvokesAllProgress()
        {
            IPromise promise1 = new Promise();
            IPromise promise2 = new Promise();

            float progress1 = 0f;
            float progress2 = 0f;

            float totalProgress = 0f;

            IPromise all = null;

            all = new Promise().All(new IPromise[]
            {
                promise1
                    .Progress(progress =>
                    {
                        progress1 = progress;
                        all.SetProgress((progress1 + progress2) / 2);
                    }),
                promise2
                    .Progress(progress =>
                    {
                        progress2 = progress;
                        all.SetProgress((progress1 + progress2) / 2);
                    })
            })
            .Progress(progress => { totalProgress = progress; });

            promise1.SetProgress(0.5f);
            promise2.SetProgress(0.3f);

            Assert.AreEqual(0.4f, totalProgress, "total progress is invalid");
            Assert.AreEqual(0.4f, all.GetProgress(), "total progress is invalid");
        }

        [TestMethod]
        public void Promise_SetProgress_InvokesMultipleProgressHandlers()
        {
            IPromise<string> promise = new Promise<string>();

            float progress1 = 0;
            float progress2 = 0;

            promise
                .Progress(delegate (float value)
                {
                    progress1 = value;
                })
                .Progress(delegate (float value)
                {
                    progress2 = value;
                });

            promise.SetProgress(0.5f);

            Assert.AreEqual(0.5f, progress1, "progress1 did not update");
            Assert.AreEqual(0.5f, progress2, "progress2 did not update");

            promise.SetProgress(0.9f);

            Assert.AreEqual(0.9f, progress1, "progress1 did not update");
            Assert.AreEqual(0.9f, progress2, "progress2 did not update");
        }

        [TestMethod]
        public void Promise_SetProgress_InvokesProgressHandler()
        {
            IPromise promise = new Promise();

            float progress = 0;
            promise.Progress(delegate (float value)
            {
                progress = value;
            });

            promise.SetProgress(0.5f);

            Assert.AreEqual(0.5f, progress, "progress did not update");
        }
        #endregion

        #region Promise.Then

        #region Promise.Then
        [TestMethod]
        public void Promise_Then_DoesNotHideExceptions()
        {
            bool didCatch = true;

            IPromise promise = new Promise();
            promise.Then(delegate
            {
                throw new Exception();
            });

            try
            {
                promise.Resolve();
            }
            catch (Exception)
            {
                didCatch = true;
            }

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void Promise_Then_DoesNotInvokeMultipleTimes_WhenAnyIsUsed_WhenMultiplePromisesResolve()
        {
            int numResolved = 0;

            IPromise promise = new Promise();
            promise.Any(new IPromise[] { GetBoolResolvedPromise(), GetBoolResolvedPromise() })
                .Then(delegate
                {
                    numResolved++;
                });

            Assert.AreEqual(1, numResolved, "Resolved multiple times");
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenAllIsUsed_WhenAllPromisesAreResolved()
        {
            bool didResolve = false;

            IPromise promise = new Promise();
            promise.All(new IPromise[] { GetResolvedPromise(), GetResolvedPromise() })
                .Then(delegate
                {
                    didResolve = true;
                });

            Assert.IsTrue(didResolve, "did not resolve resulting promise");
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenAnyIsUsed()
        {
            bool didResolve = false;

            IPromise a = new Promise();
            a.Resolve();

            IPromise b = new Promise();

            IPromise any = new Promise();
            any.Any(new IPromise[] { a, b }).Then(delegate
            {
                didResolve = true;
            });

            Assert.AreEqual(didResolve, true);
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenChained_WhenPromiseIsResolved()
        {
            bool didResolve = false;

            IPromise a = new Promise();
            a.Resolve();

            a.Chain(GetBoolResolvedPromise)
                .Then(delegate (bool val)
                {
                    didResolve = val;
                });

            Assert.AreEqual(didResolve, true);
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenChainedPromiseIsResolved()
        {
            bool didResolve = false;

            IPromise a = GetResolvedPromise();
            a.Chain(GetResolvedPromise)
            .Then(delegate
            {
                didResolve = true;
            });

            Assert.AreEqual(didResolve, true);
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenPromiseIsResolved()
        {
            bool didResolve = false;

            IPromise promise = GetResolvedPromise();

            promise.Then(delegate
            {
                didResolve = true;
            });

            Assert.AreEqual(didResolve, true);
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenPromiseIsResolved_WithOneProperty()
        {
            bool didResolve = false;
            string p1 = null;

            IPromise promise = GetResolvedPromise();

            promise.Then(delegate (string property1)
            {
                didResolve = true;
                p1 = property1;
            }, "abc");

            Assert.IsTrue(didResolve, "Did not resolve");
            Assert.AreEqual("abc", p1, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenPromiseIsResolved_WithTwoProperties()
        {
            bool didResolve = false;
            string p1 = null;
            string p2 = null;

            IPromise promise = GetResolvedPromise();

            promise.Then(delegate (string property1, string property2)
            {
                didResolve = true;
                p1 = property1;
                p2 = property2;
            }, "abc", "def");

            Assert.IsTrue(didResolve, "Did not resolve");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenPromiseIsResolved_WithThreeProperties()
        {
            bool didResolve = false;
            string p1 = null;
            string p2 = null;
            string p3 = null;

            IPromise promise = GetResolvedPromise();

            promise.Then(delegate (string property1, string property2, string property3)
            {
                didResolve = true;
                p1 = property1;
                p2 = property2;
                p3 = property3;
            }, "abc", "def", "ghi");

            Assert.IsTrue(didResolve, "Did not resolve");
            Assert.AreEqual("abc", p1, "Did not propagate property");
            Assert.AreEqual("def", p2, "Did not propagate property");
            Assert.AreEqual("ghi", p3, "Did not propagate property");
        }


        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenPromiseIsResolved_WhenCastToIPromiseBase()
        {
            bool didResolve = false;

            IPromiseBase promise = (IPromiseBase)GetResolvedPromise();

            promise.Then(delegate
            {
                didResolve = true;
            });

            Assert.AreEqual(didResolve, true);
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenPromiseIsResolvedImmediately()
        {
            bool didResolve = false;

            GetResolvedPromise().Then(delegate
            {
                didResolve = true;
            });

            Assert.IsTrue(didResolve, "did not resolve");
        }
        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenSequencedPromiseIsResolved()
        {
            int result = 0;

            IPromise<int> a = new Promise<int>();
            a.Resolve(1);
            IPromise<int> b = new Promise<int>();
            b.Resolve(2);

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { () => a, () => b })
                .Then(delegate (IEnumerable<IPromise> promises)
                {
                    foreach (IPromise<int> promise in promises)
                    {
                        result += promise.Value;
                    }
                });

            Assert.AreEqual(result, 3);
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenSequencedPromiseIsResolved_WithCorrectOrder()
        {
            bool didResolve = false;

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { GetBoolResolvedPromise, GetBoolResolvedPromise, GetIntResolvedPromise })
                .Then(delegate (IEnumerable<IPromise> promises)
                {
                    for (int i = 0; i < promises.Count(); i++)
                    {
                        if (i == 2)
                        {
                            IPromise<int> intPromise = (IPromise<int>)promises.ElementAt(i);

                            if (intPromise != null && intPromise.Value == 1)
                            {
                                didResolve = true;
                            }
                        }
                    }
                });

            Assert.AreEqual(didResolve, true);
        }

        [TestMethod]
        public void Promise_Then_InvokesCallback_WhenSequencedPromiseResolves()
        {
            bool sequencedPromiseResolved = false;
            bool sequenceResolved = false;

            IPromise<int> a = new Promise<int>();

            a.Then(delegate (int value)
            {
                sequencedPromiseResolved = true;
            });

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { () => a })
                .Then(delegate (IEnumerable<IPromise> promises)
                {
                    Assert.IsTrue(sequencedPromiseResolved, "Sequenced promise is not resolved");
                    sequenceResolved = true;
                });


            a.Resolve(2);

            Assert.IsTrue(sequenceResolved, "Sequence is not resolved");
        }
        #endregion

        #region Promise.Then<T>
        [TestMethod]
        public void Promise_GenericThen_InvokesCallback_WhenPromiseIsResolved()
        {
            IPromise promise = GetResolvedPromise();
            IPromise<int> second = promise.Then(delegate
            {
                return 123;
            });

            Assert.AreEqual(PromiseState.Resolved, second.State, "Resulting Promise<T> did not resolve");
            Assert.AreEqual(123, second.Value, "Callback value did not propagate to the resulting Promise<T>");
        }

        [TestMethod]
        public void Promise_GenericThen_PropagatesFailureCallback()
        {
            bool didCatch = false;
            var exc = new Exception();

            IPromise promise = GetFailedPromise(exc);
            IPromise<int> second = promise.Then(delegate
            {
                return 123;
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(PromiseState.Failed, second.State, "Resulting Promise<T> did not fail");
            Assert.IsTrue(didCatch, "Did not catch exception");
            Assert.AreEqual(exc, second.Error, "Exception did not propagate properly to the resulting Promise<T>");
        }

        [TestMethod]
        public void Promise_GenericThen_PropagatesCancellationHandler()
        {
            bool didRequestToCancel = false;

            IPromise promise = GetPromise();
            IPromise<int> second = promise.Then(delegate
            {
                return 123;
            });

            promise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                didRequestToCancel = true;
            };

            second.RequestCancel();

            Assert.IsTrue(didRequestToCancel, "Did not propagate cancellation request");
        }
        #endregion

        #endregion

        #region Promise.UncaughtExceptionHandler
        [TestMethod]
        public void Promise_UncaughtExceptionHandler_DoesNotInvoke_WhenExceptionIsCaught()
        {
            bool exceptionCaught = false;
            bool defaultExceptionHandlerCaught = false;
            IPromise promise = new Promise();

            UncaughtExceptionHandler handler = delegate (object sender, UncaughtExceptionEventArgs e)
            {
                defaultExceptionHandlerCaught = true;
            };

            Promise.UncaughtExceptionThrown += handler;

            try
            {
                promise.Catch(delegate (Exception e)
                {
                    exceptionCaught = true;
                });

                promise.Fail(new Exception());

                Assert.AreEqual(exceptionCaught, true);
                Assert.AreEqual(defaultExceptionHandlerCaught, false);
            }
            finally
            {
                Promise.UncaughtExceptionThrown -= handler;
            }
        }

        [TestMethod]
        public void Promise_UncaughtExceptionHandler_Invoked_WhenExceptionIsUnhandled()
        {
            bool exceptionCaught = false;
            bool defaultExceptionHandlerCaught = false;
            IPromise promise = new Promise();

            UncaughtExceptionHandler handler = delegate (object sender, UncaughtExceptionEventArgs e)
            {
                defaultExceptionHandlerCaught = true;
            };

            Promise.UncaughtExceptionThrown += handler;

            try
            {
                promise.Catch(delegate (ArgumentException e)
                {
                    exceptionCaught = true;
                })
                .Done();

                promise.Fail(new Exception());

                Assert.AreEqual(exceptionCaught, false);
                Assert.AreEqual(defaultExceptionHandlerCaught, true);
            }
            finally
            {
                Promise.UncaughtExceptionThrown -= handler;
            }
        }

        #endregion

        #region Promise.StateChangedHandler
        [TestMethod]
        public void Promise_StateChangedHandler_Invoked_WhenResolved()
        {
            var promise = new Promise();
            var didBeforeTransition = false;

            PromiseStateChangedHandler callback = null;
            callback = (sender, e) =>
            {
                Assert.IsNotNull(e.Promise, "Promise is null");

                if (!didBeforeTransition)
                {
                    Assert.AreEqual(PromiseTransistionState.ResolvedBeforeCallbacks, e.PromiseTransistionState, "Expected ResolvedBeforeCallbacks transition type");
                    didBeforeTransition = true;
                }
                else
                {
                    Assert.AreEqual(PromiseTransistionState.ResolvedAfterCallbacks, e.PromiseTransistionState, "Expected ResolvedAfterCallbacks transition type");
                    Promise.PromiseStateChanged -= callback;
                }
            };
            Promise.PromiseStateChanged += callback;

            promise.Resolve();
        }

        [TestMethod]
        public void Promise_StateChangedHandler_Invoked_WhenFailed()
        {
            var promise = new Promise();
            var didBeforeTransition = false;

            PromiseStateChangedHandler callback = null;
            callback = (sender, e) =>
            {
                Assert.IsNotNull(e.Promise, "Promise is null");

                if (!didBeforeTransition)
                {
                    Assert.AreEqual(PromiseTransistionState.FailedBeforeCallbacks, e.PromiseTransistionState, "Expected FailedBeforeCallbacks transition type");
                    didBeforeTransition = true;
                }
                else
                {
                    Assert.AreEqual(PromiseTransistionState.FailedAfterCallbacks, e.PromiseTransistionState, "Expected FailedAfterCallbacks transition type");
                    Promise.PromiseStateChanged -= callback;
                }
            };
            Promise.PromiseStateChanged += callback;

            promise.Fail(new Exception());
        }

        [TestMethod]
        public void Promise_StateChangedHandler_Invoked_WhenInitialized()
        {
            PromiseStateChangedHandler callback = null;
            callback = (sender, e) =>
            {
                Assert.IsNotNull(e.Promise, "Promise is null");
                Assert.AreEqual(PromiseTransistionState.Initialized, e.PromiseTransistionState);
                Promise.PromiseStateChanged -= callback;
            };

            Promise.PromiseStateChanged += callback;

            var promise = new Promise();
        }
        #endregion
        #endregion

        #region Promise<T>
        #region Promise<T>.All
        [TestMethod]
        public void GenericPromise_All_DoesReturnsSelf()
        {
            IPromise<bool> promise = new Promise<bool>();
            IPromise promise2 = promise.All(new IPromise[] { GetBoolResolvedPromise() });
            Assert.AreEqual(promise, promise2);
        }
        #endregion

        #region Promise<T>.Catch
        [TestMethod]
        public void GenericPromise_Catch_InvokesCallback_WhenAllIsUsed_WhenOnePromiseFails()
        {
            bool didFail = false;

            IPromise<bool> promise = new Promise<bool>();
            promise.All(new IPromise[] { GetFailedPromise(), GetResolvedPromise() })
                .Catch(delegate
                {
                    didFail = true;
                });

            Assert.IsTrue(didFail, "resulting promise did not fail");
        }

        [TestMethod]
        public void GenericPromise_Catch_InvokesCallback_WhenFirstSequencedMethodFailed()
        {
            bool didCatch = false;

            IPromise<IEnumerable<IPromise>> promise = new Promise<IEnumerable<IPromise>>();
            promise.Sequence(new Func<IPromise>[]
            {
                () => GetBoolFailedPromise(),
                () => GetBoolResolvedPromise()
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }


        [TestMethod]
        public void GenericPromise_Catch_InvokesCallback_WhenSecondSequencedMethodFailed()
        {
            bool didCatch = false;

            IPromise<IEnumerable<IPromise>> promise = new Promise<IEnumerable<IPromise>>();
            promise.Sequence(new Func<IPromise>[]
            {
                () => GetResolvedPromise(),
                () => GetFailedPromise(new Exception())
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void GenericPromise_Catch_InvokesCallback_WhenThirdSequencedMethodFailed()
        {
            bool didCatch = false;

            IPromise<IEnumerable<IPromise>> promise = new Promise<IEnumerable<IPromise>>();
            promise.Sequence(new Func<IPromise>[]
            {
                () => GetResolvedPromise(),
                () => GetResolvedPromise(),
                () => GetFailedPromise(new Exception())
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(didCatch, true);
        }

        #endregion

        #region Promise<T>.Chain

        #region Promise<T>.Chain
        [TestMethod]
        public void GenericPromise_NonGenericChain_InvokesChainedMethod_WhenFirstPromiseResolved()
        {
            bool didPropagate = false;

            var promise = GetBoolPromise();

            promise.Chain((boolValue) =>
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                return secondPromise;
            });

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
        }

        [TestMethod]
        public void GenericPromise_NonGenericChain_InvokesChainedMethod_WhenFirstPromiseResolved_WithOneProperty()
        {
            bool didPropagate = false;
            string property1 = null;

            var promise = GetBoolPromise();

            promise.Chain(delegate (bool boolValue, string p1)
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                property1 = p1;
                return secondPromise;
            }, "property1");

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("property1", property1, "First property did not propagate");
        }

        [TestMethod]
        public void GenericPromise_NonGenericChain_InvokesChainedMethod_WhenFirstPromiseResolved_WithTwoProperties()
        {
            bool didPropagate = false;
            string property1 = null;
            string property2 = null;

            var promise = GetBoolPromise();

            promise.Chain(delegate (bool boolValue, string p1, string p2)
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                property1 = p1;
                property2 = p2;
                return secondPromise;
            }, "property1", "property2");

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("property1", property1, "First property did not propagate");
            Assert.AreEqual("property2", property2, "Second property did not propagate");
        }

        [TestMethod]
        public void GenericPromise_NonGenericChain_InvokesChainedMethod_WhenFirstPromiseResolved_WithThreeProperties()
        {
            bool didPropagate = false;
            string property1 = null;
            string property2 = null;
            string property3 = null;

            var promise = GetBoolPromise();

            promise.Chain(delegate (bool boolValue, string p1, string p2, string p3)
            {
                var secondPromise = GetResolvedPromise();
                didPropagate = true;
                property1 = p1;
                property2 = p2;
                property3 = p3;
                return secondPromise;
            }, "property1", "property2", "property3");

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
            Assert.AreEqual("property1", property1, "First property did not propagate");
            Assert.AreEqual("property2", property2, "Second property did not propagate");
            Assert.AreEqual("property3", property3, "Third property did not propagate");
        }

        [TestMethod]
        public void GenericPromise_NonGenericChain_PropagatesCatchHandler()
        {
            bool didCatch = false;
            var promise = GetBoolPromise();
            promise.Chain(delegate (bool value)
            {
                return GetPromise();
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            promise.Fail(new Exception());

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void GenericPromise_NonGenericChain_PropagatesCancellationHandler()
        {
            bool requestedToCancel = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancel = true;
            };

            var resultPromise = firstPromise.Chain(delegate (bool value)
            {
                return GetPromise();
            });

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancel, true, "did not request to cancel");
        }

        [TestMethod]
        public void GenericPromise_NonGenericChain_PropagatesCancellationHandler_WhenFirstPromiseResolved()
        {
            bool requestedToCancelFirstPromise = false;
            bool requestedToCancelSecondPromise = false;

            var firstPromise = new Promise<bool>();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancelFirstPromise = true;
            };

            var resultPromise = firstPromise.Chain(delegate (bool value)
            {
                var secondPromise = GetPromise();
                secondPromise.CancelRequested += (sender, e) =>
                {
                    requestedToCancelSecondPromise = true;
                };
                return secondPromise;
            });

            firstPromise.Resolve(true);

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancelFirstPromise, false, "requested to cancel first promise when it shouldn't have");
            Assert.AreEqual(requestedToCancelSecondPromise, true, "did not request to cancel second promise");
        }

        #endregion

        #region Promise<T>.Chain<T2>
        [TestMethod]
        public void GenericPromise_GenericChain_InvokesChainedMethod_WhenFirstPromiseResolved()
        {
            bool didPropagate = false;

            var promise = GetBoolPromise();

            promise.Chain(() =>
            {
                var secondPromise = GetIntResolvedPromise();
                didPropagate = true;
                return secondPromise;
            });

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
        }

        [TestMethod]
        public void GenericPromise_GenericChain_PropagatesCatchHandler()
        {
            bool didCatch = false;
            var promise = GetBoolPromise();
            promise.Chain(GetIntPromise)
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            promise.Fail(new Exception());

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void GenericPromise_GenericChain_PropagatesCancellationHandler()
        {
            bool requestedToCancel = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancel = true;
            };

            var resultPromise = firstPromise.Chain(GetIntPromise);
            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancel, true, "did not request to cancel");
        }

        [TestMethod]
        public void GenericPromise_GenericChain_PropagatesCancellationHandler_WhenFirstPromiseResolved()
        {
            bool requestedToCancelFirstPromise = false;
            bool requestedToCancelSecondPromise = false;

            var firstPromise = GetPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancelFirstPromise = true;
            };

            var resultPromise = firstPromise.Chain(() =>
            {
                var secondPromise = GetIntPromise();
                secondPromise.CancelRequested += (sender, e) =>
                {
                    requestedToCancelSecondPromise = true;
                };
                return secondPromise;
            });

            firstPromise.Resolve();

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancelFirstPromise, false, "requested to cancel first promise when it shouldn't have");
            Assert.AreEqual(requestedToCancelSecondPromise, true, "did not request to cancel second promise");
        }
        #endregion

        #region Promise<T>.Chain<T2, P1>
        [TestMethod]
        public void GenericPromise_GenericChainOneProperty_InvokesChainedMethod_WhenFirstPromiseResolved()
        {
            bool didPropagate = false;

            var promise = GetBoolPromise();

            promise.Chain(delegate (bool value, string property1)
            {
                var secondPromise = GetIntResolvedPromise();
                didPropagate = true;
                return secondPromise;
            }, "abc");

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
        }

        [TestMethod]
        public void GenericPromise_GenericChainOneProperty_PropagatesCatchHandler()
        {
            bool didCatch = false;
            var promise = GetBoolPromise();
            promise.Chain((value, property1) => GetIntResolvedPromise(), "abc")
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            promise.Fail(new Exception());

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void GenericPromise_GenericChainOneProperty_PropagatesCancellationHandler()
        {
            bool requestedToCancel = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancel = true;
            };

            var resultPromise = firstPromise.Chain((value, property1) => GetIntPromise(), "abc");
            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancel, true, "did not request to cancel");
        }

        [TestMethod]
        public void GenericPromise_GenericChainOneProperty_PropagatesCancellationHandler_WhenFirstPromiseResolved()
        {
            bool requestedToCancelFirstPromise = false;
            bool requestedToCancelSecondPromise = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancelFirstPromise = true;
            };

            var resultPromise = firstPromise.Chain((value, property1) =>
            {
                var secondPromise = GetIntPromise();
                secondPromise.CancelRequested += (sender, e) =>
                {
                    requestedToCancelSecondPromise = true;
                };
                return secondPromise;
            }, "abc");

            firstPromise.Resolve(true);

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancelFirstPromise, false, "requested to cancel first promise when it shouldn't have");
            Assert.AreEqual(requestedToCancelSecondPromise, true, "did not request to cancel second promise");
        }
        #endregion

        #region Promise<T>.Chain<T2, P1, P2>
        [TestMethod]
        public void GenericPromise_GenericChainTwoProperties_InvokesChainedMethod_WhenFirstPromiseResolved()
        {
            bool didPropagate = false;

            var promise = GetBoolPromise();

            promise.Chain(delegate (bool value, string property1, float property2)
            {
                var secondPromise = GetIntResolvedPromise();
                didPropagate = true;
                return secondPromise;
            }, "abc", 0.1f);

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
        }

        [TestMethod]
        public void GenericPromise_GenericChainTwoProperties_PropagatesCatchHandler()
        {
            bool didCatch = false;
            var promise = GetBoolPromise();
            promise.Chain((value, property1, property2) => GetIntResolvedPromise(), "abc", 0.1f)
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            promise.Fail(new Exception());

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void GenericPromise_GenericChainTwoProperties_PropagatesCancellationHandler()
        {
            bool requestedToCancel = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancel = true;
            };

            var resultPromise = firstPromise.Chain((value, property1, property2) => GetIntPromise(), "abc", 0.1f);
            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancel, true, "did not request to cancel");
        }

        [TestMethod]
        public void GenericPromise_GenericChainTwoProperties_PropagatesCancellationHandler_WhenFirstPromiseResolved()
        {
            bool requestedToCancelFirstPromise = false;
            bool requestedToCancelSecondPromise = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancelFirstPromise = true;
            };

            var resultPromise = firstPromise.Chain((value, property1, property2) =>
            {
                var secondPromise = GetIntPromise();
                secondPromise.CancelRequested += (sender, e) =>
                {
                    requestedToCancelSecondPromise = true;
                };
                return secondPromise;
            }, "abc", 0.1f);

            firstPromise.Resolve(true);

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancelFirstPromise, false, "requested to cancel first promise when it shouldn't have");
            Assert.AreEqual(requestedToCancelSecondPromise, true, "did not request to cancel second promise");
        }
        #endregion

        #region Promise<T>.Chain<T2, P1, P2, P3>

        [TestMethod]
        public void GenericPromise_GenericChainThreeProperties_InvokesChainedMethod_WhenFirstPromiseResolved()
        {
            bool didPropagate = false;

            var promise = GetBoolPromise();

            promise.Chain(delegate (bool value, string property1, float property2, double property3)
            {
                var secondPromise = GetIntResolvedPromise();
                didPropagate = true;
                return secondPromise;
            }, "abc", 0.1f, 0.2d);

            promise.Resolve(true);

            Assert.IsTrue(didPropagate, "did not propagate");
        }

        [TestMethod]
        public void GenericPromise_GenericChainThreeProperties_PropagatesCatchHandler()
        {
            bool didCatch = false;
            var promise = GetBoolPromise();
            promise.Chain((value, property1, property2, property3) => GetIntResolvedPromise(), "abc", 0.1f, 0.2d)
                .Catch(delegate (Exception e)
                {
                    didCatch = true;
                });

            promise.Fail(new Exception());

            Assert.AreEqual(didCatch, true);
        }

        [TestMethod]
        public void GenericPromise_GenericChainThreeProperties_PropagatesCancellationHandler()
        {
            bool requestedToCancel = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancel = true;
            };

            var resultPromise = firstPromise.Chain((value, property1, property2, property3) => GetIntPromise(), "abc", 0.1f, 0.2d);
            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancel, true, "did not request to cancel");
        }

        [TestMethod]
        public void GenericPromise_GenericChainThreeProperties_PropagatesCancellationHandler_WhenFirstPromiseResolved()
        {
            bool requestedToCancelFirstPromise = false;
            bool requestedToCancelSecondPromise = false;

            var firstPromise = GetBoolPromise();
            firstPromise.CancelRequested += (sender, e) =>
            {
                requestedToCancelFirstPromise = true;
            };

            var resultPromise = firstPromise.Chain((value, property1, property2, property3) =>
            {
                var secondPromise = GetIntPromise();
                secondPromise.CancelRequested += (sender, e) =>
                {
                    requestedToCancelSecondPromise = true;
                };
                return secondPromise;
            }, "abc", 0.1f, 0.2d);

            firstPromise.Resolve(true);

            resultPromise.RequestCancel();

            Assert.AreEqual(requestedToCancelFirstPromise, false, "requested to cancel first promise when it shouldn't have");
            Assert.AreEqual(requestedToCancelSecondPromise, true, "did not request to cancel second promise");
        }
        #endregion

        #endregion

        #region Promise<T>.Failed
        [TestMethod]
        public void GenericPromise_Failed_ReturnsFailedPromise()
        {
            Exception e = new Exception("some error");
            IPromise<string> promise = Promise<string>.Failed(e);

            Assert.AreEqual(PromiseState.Failed, promise.State, "promise did not fail properly");

            bool didCatch = false;

            promise.Catch(delegate (Exception exc)
            {
                didCatch = true;

                Assert.AreEqual(e, exc, "promise exception is invalid");
            });

            Assert.IsTrue(didCatch, "promise did not catch");
        }
        #endregion

        #region Promise<T>.Finally
        [TestMethod]
        public void GenericPromise_Finally_DoesNotHideException_WhenPromiseFailed()
        {
            bool didCatch = false;

            GetBoolFailedPromise(new Exception())
                .Finally(() => { })
                .Catch<Exception>(delegate (Exception e)
                {
                    didCatch = true;
                });

            Assert.IsTrue(didCatch, "did not catch");
        }

        [TestMethod]
        public void GenericPromise_Finally_DoesNotHideException_WhenThrown()
        {
            bool defaultExceptionHandlerCaught = false;

            UncaughtExceptionHandler handler = delegate (object sender, UncaughtExceptionEventArgs e)
            {
                defaultExceptionHandlerCaught = true;
            };

            Promise.UncaughtExceptionThrown += handler;

            try
            {
                GetBoolFailedPromise(new Exception())
                    .Finally(() => { })
                    .Done();
            }
            finally
            {
                Promise.UncaughtExceptionThrown -= handler;
            }

            Assert.IsTrue(defaultExceptionHandlerCaught, "Did not invoke uncaught exception handler");
        }

        [TestMethod]
        public void GenericPromise_Finally_Invokes_WhenPromiseFailed()
        {
            bool didInvoke = false;

            GetBoolFailedPromise(new Exception())
                .Finally(() =>
                {
                    didInvoke = true;
                });

            Assert.IsTrue(didInvoke, "did not invoke Finally()");
        }

        [TestMethod]
        public void GenericPromise_Finally_Invokes_WhenSequenceIsFailed()
        {
            bool didCatch = false;
            bool didInvokeFinally = false;

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { GetBoolFailedPromise })
                .Catch(delegate
                {
                    didCatch = true;
                })
                .Finally(delegate
                {
                    didInvokeFinally = true;
                });

            Assert.IsTrue(didCatch, "did not catch failed promise");
            Assert.IsTrue(didInvokeFinally, "did not invoke Finally");
        }

        [TestMethod]
        public void GenericPromise_Finally_InvokesCallback()
        {
            bool didInvoke = false;

            GetBoolResolvedPromise()
                .Finally(() =>
                {
                    didInvoke = true;
                });

            Assert.IsTrue(didInvoke, "did not invoke Finally()");
        }

        [TestMethod]
        public void GenericPromise_Finally_ReturnsPromise()
        {
            IPromise<bool> promise = GetBoolResolvedPromise()
                .Finally(() => { });

            Assert.AreNotEqual(null, promise, "returned value is null");
        }
        #endregion

        #region Promise<T>.RequestCancel
        [TestMethod]
        public void GenericPromise_RequestCancel_InvokesAllCallbacks_WhenAllIsUsed()
        {
            int numCancelled = 0;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                numCancelled++;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                numCancelled++;
            };

            IPromise promise = new Promise<IEnumerable<IPromise>>().All(new IPromise[] { promise1, promise2 });

            promise.RequestCancel();

            Assert.AreEqual(2, numCancelled, "All() did not pass RequestCancel");
        }

        [TestMethod]
        public void GenericPromise_RequestCancel_InvokesCallback_OnActiveSequencedPromise()
        {
            bool firstCancelled = false, secondCancelled = false;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                firstCancelled = true;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                secondCancelled = true;
            };

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>().Sequence(new Func<IPromise>[] { () => { return promise1; }, () => { return promise2; } });
            sequence.RequestCancel();

            Assert.IsTrue(firstCancelled, "did not request cancel on a first promise");
            Assert.IsFalse(secondCancelled, "did request cancel on a second promise");
        }

        [TestMethod]
        public void GenericPromise_RequestCancel_InvokesCallback_OnActiveSequencedPromise_WhenFirstPromiseIsResolved()
        {
            bool firstCancelled = false, secondCancelled = false;

            IPromise promise1 = GetPromise();
            promise1.CancelRequested += delegate
            {
                firstCancelled = true;
            };

            IPromise promise2 = GetPromise();
            promise2.CancelRequested += delegate
            {
                secondCancelled = true;
            };

            promise1.Resolve(); // Resolve first promise, second promise is active in a sequence.

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>().Sequence(new Func<IPromise>[] { () => { return promise1; }, () => { return promise2; } });
            sequence.RequestCancel();

            Assert.IsFalse(firstCancelled, "did request cancel on a first promise");
            Assert.IsTrue(secondCancelled, "did not request cancel on a second promise");
        }

        [TestMethod]
        public void GenericPromise_RequestCancel_InvokesCallback_WhenGenericThenIsUsed()
        {
            bool didInvoke = false;

            IPromise<int> promise = new Promise<int>();
            IPromise<string> second = promise.Then(delegate (int value)
            {
                return "value=" + value.ToString();
            });

            promise.CancelRequested += delegate
            {
                didInvoke = true;
            };

            second.RequestCancel();

            Assert.IsTrue(didInvoke, "did not propagate cancellation callback");
        }

        #endregion

        #region Promise<T>.Resolve
        [TestMethod]
        public void GenericPromise_Resolve_InvokesCallback()
        {
            bool didResolve = false;

            IPromise<bool> promise = GetBoolPromise();
            promise.Then(delegate (bool value)
            {
                didResolve = true;
            });

            promise.Resolve(true);

            Assert.IsTrue(didResolve, "did not invoke");
        }

        [TestMethod]
        public void GenericPromise_Resolve_InvokesGenericThenCallback()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise<int> promise = new Promise<int>();
            IPromise<string> second = promise.Then(delegate (int value)
            {
                return "value=" + value.ToString();
            });
            second.Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve(321);

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("value=321", resolvedValue, "Did not resolve second promise with correct value");
        }

        [TestMethod]
        public void GenericPromise_Resolve_InvokesGenericThenCallback_WithOneProperty()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise<int> promise = new Promise<int>();
            IPromise<string> second = promise.Then(delegate (int value, string property1)
            {
                return $"{value}, {property1}";
            }, "abc")
            .Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve(321);

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("321, abc", resolvedValue, "Did not resolve second promise with correct value");
        }

        [TestMethod]
        public void GenericPromise_Resolve_InvokesGenericThenCallback_WithTwoProperties()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise<int> promise = new Promise<int>();
            IPromise<string> second = promise.Then(delegate (int value, string property1, string property2)
            {
                return $"{value}, {property1}, {property2}";
            }, "abc", "def")
            .Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve(321);

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("321, abc, def", resolvedValue, "Did not resolve second promise with correct value");
        }

        [TestMethod]
        public void GenericPromise_Resolve_InvokesGenericThenCallback_WithThreeProperties()
        {
            bool didResolve = false;
            string resolvedValue = null;

            IPromise<int> promise = new Promise<int>();
            IPromise<string> second = promise.Then(delegate (int value, string property1, string property2, string property3)
            {
                return $"{value}, {property1}, {property2}, {property3}";
            }, "abc", "def", "ghi")
            .Then(delegate (string value)
            {
                didResolve = true;
                resolvedValue = value;
            });

            promise.Resolve(321);

            Assert.AreEqual(true, didResolve, "Did not resolve");
            Assert.AreEqual("321, abc, def, ghi", resolvedValue, "Did not resolve second promise with correct value");
        }
        #endregion

        #region Promise<T>.Resolved
        [TestMethod]
        public void GenericPromise_Resolved_ReturnsResolvedPromise()
        {
            IPromise<string> promise = Promise<string>.Resolved("value");

            Assert.AreEqual(PromiseState.Resolved, promise.State, "promise is not resolved");
            Assert.AreEqual("value", promise.Value, "promise value is invalid");

            bool didResolve = false;
            promise.Then((string value) =>
            {
                Assert.AreEqual("value", value, "promise value is invalid");

                didResolve = true;
            });

            Assert.IsTrue(didResolve, "promise did not resolve");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GenericPromise_Resolve_ThrowsInvalidOperationException_WhenPromiseIsAlreadyFailed()
        {
            IPromise<string> promise = new Promise<string>();
            promise.Fail(new Exception("Exception"));
            promise.Resolve("value");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GenericPromise_Resolve_ThrowsInvalidOperationException_WhenPromiseIsAlreadyResolved()
        {
            IPromise<string> promise = new Promise<string>();
            promise.Resolve("value 1");
            promise.Resolve("value 2");
        }
        #endregion

        #region Promise<T>.Sequence
        [TestMethod]
        public void GenericPromise_Sequence_DoesReturnsThis()
        {
            IPromise<IEnumerable<IPromise>> promise = new Promise<IEnumerable<IPromise>>();
            IPromise promise2 = promise.Sequence(new Func<IPromise>[] { GetBoolResolvedPromise });
            Assert.AreEqual(promise, promise2);
        }
        #endregion

        #region Promise<T>.Sequence
        [TestMethod]
        public void GenericPromise_Sequence_DoesReturnThis_WithLambda()
        {
            IPromise<IEnumerable<IPromise>> promise = new Promise<IEnumerable<IPromise>>();
            IPromise promise2 = promise.Sequence(new Func<IPromise>[] { () => GetBoolResolvedPromise() });
            Assert.AreEqual(promise, promise2);
        }
        #endregion

        #region Promise<T>.Then
        #region Promise.Then
        [TestMethod]
        public void GenericPromise_Then_InvokesCallback_WhenAllIsUsed_WhenAllPromisesAreResolved()
        {
            bool didResolve = false;

            IPromise<bool> promise = new Promise<bool>();
            promise.All(new IPromise[] { GetResolvedPromise(), GetResolvedPromise() })
                .Then(delegate
                {
                    didResolve = true;
                });

            Assert.IsTrue(didResolve, "did not resolve resulting promise");
        }

        [TestMethod]
        public void GenericPromise_Then_InvokesCallback_WhenPromiseIsResolved()
        {
            bool didResolve = false;

            GetBoolResolvedPromise(true).Then(delegate (bool value)
            {
                Assert.AreEqual(value, true);
                didResolve = true;
            });

            Assert.IsTrue(didResolve, "did not invoke");
        }

        [TestMethod]
        public void GenericPromise_Then_InvokesCallback_WhenPromiseIsResolved_WhenCastIPromiseBase()
        {
            bool didResolve = false;
            var promise = (IPromiseBase<bool>)GetBoolResolvedPromise(true);
            promise.Then(delegate (bool value)
            {
                Assert.AreEqual(value, true);
                didResolve = true;
            });

            Assert.IsTrue(didResolve, "did not invoke");
        }
        #endregion

        #region Promise<T>.Then<T2>
        [TestMethod]
        public void GenericPromise_GenericThen_InvokesCallback_WhenPromiseIsResolved()
        {
            IPromise<int> promise = GetIntResolvedPromise(123);
            IPromise<string> second = promise.Then(delegate (int value)
            {
                return "value=" + value.ToString();
            });

            Assert.AreEqual(PromiseState.Resolved, second.State, "Resulting Promise<T> did not resolve");
            Assert.AreEqual("value=123", second.Value, "Callback value did not propagate to the resulting Promise<T>");
        }

        [TestMethod]
        public void GenericPromise_GenericThen_InvokesCallback_WhenPromiseIsResolved_WithOneProperty()
        {
            IPromise<int> promise = GetIntResolvedPromise(123);
            IPromise<string> second = promise.Then(delegate (int value, string property1)
            {
                return $"{value}, {property1}";
            }, "abc");

            Assert.AreEqual(PromiseState.Resolved, second.State, "Resulting Promise<T> did not resolve");
            Assert.AreEqual("123, abc", second.Value, "Callback value did not propagate to the resulting Promise<T>");
        }

        [TestMethod]
        public void GenericPromise_GenericThen_InvokesCallback_WhenPromiseIsResolved_WithTwoProperties()
        {
            IPromise<int> promise = GetIntResolvedPromise(123);
            IPromise<string> second = promise.Then(delegate (int value, string property1, string property2)
            {
                return $"{value}, {property1}, {property2}";
            }, "abc", "def");

            Assert.AreEqual(PromiseState.Resolved, second.State, "Resulting Promise<T> did not resolve");
            Assert.AreEqual("123, abc, def", second.Value, "Callback value did not propagate to the resulting Promise<T>");
        }

        [TestMethod]
        public void GenericPromise_GenericThen_InvokesCallback_WhenPromiseIsResolved_WithThreeProperties()
        {
            IPromise<int> promise = GetIntResolvedPromise(123);
            IPromise<string> second = promise.Then(delegate (int value, string property1, string property2, string property3)
            {
                return $"{value}, {property1}, {property2}, {property3}";
            }, "abc", "def", "ghi");

            Assert.AreEqual(PromiseState.Resolved, second.State, "Resulting Promise<T> did not resolve");
            Assert.AreEqual("123, abc, def, ghi", second.Value, "Callback value did not propagate to the resulting Promise<T>");
        }

        [TestMethod]
        public void GenericPromise_GenericThen_PropagatesFailureCallback()
        {
            bool didCatch = false;
            var exc = new Exception();

            IPromise<int> promise = GetFailedGenericPromise<int>(exc);
            IPromise<string> second = promise.Then(delegate (int value)
            {
                return "value=" + value.ToString();
            })
            .Catch(delegate (Exception e)
            {
                didCatch = true;
            });

            Assert.AreEqual(PromiseState.Failed, second.State, "Resulting Promise<T> did not fail");
            Assert.IsTrue(didCatch, "Did not catch exception");
            Assert.AreEqual(exc, second.Error, "Exception did not propagate properly to the resulting Promise<T>");
        }

        [TestMethod]
        public void GenericPromise_GenericThen_PropagatesCancellationHandler()
        {
            bool didRequestToCancel = false;

            IPromise<int> promise = GetIntPromise();
            IPromise<string> second = promise.Then(delegate (int value)
            {
                return "value=" + value.ToString();
            });

            promise.CancelRequested += delegate (object sender, PromiseCancelRequestedEventArgs e)
            {
                didRequestToCancel = true;
            };

            second.RequestCancel();

            Assert.IsTrue(didRequestToCancel, "Did not propagate cancellation request");
        }
        #endregion
        #endregion

        #region Promise<T>.Resolve
        [TestMethod]
        public void GenericPromise_Resolve_InvokesCallback_WhenSequenced_WithPromiseList()
        {
            int result = 0;

            IPromise<int> a = new Promise<int>();
            IPromise<int> b = new Promise<int>();

            IPromise<IEnumerable<IPromise>> sequence = new Promise<IEnumerable<IPromise>>();
            sequence.Sequence(new Func<IPromise>[] { () => a, () => b })
                .Then(delegate (IEnumerable<IPromise> promises)
                {
                    foreach (IPromise<int> promise in promises)
                    {
                        result += promise.Value;
                    }
                });

            a.Resolve(1);
            b.Resolve(2);

            Assert.AreEqual(result, 3);
        }


        [TestMethod]
        public void GenericPromise_Resolve_ClearsResolveHandlers()
        {
            WeakReference weakReference = null;

            IPromise<string> promise = new Promise<string>();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Then((str) =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve("aaa");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void GenericPromise_Resolve_ClearsPureResolveHandlers()
        {
            WeakReference weakReference = null;

            IPromise<string> promise = new Promise<string>();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Then(() =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve("aaa");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void GenericPromise_Resolve_ClearsCatchHandlers()
        {
            WeakReference weakReference = null;

            IPromise<string> promise = new Promise<string>();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Catch((exception) =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve("aaa");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void GenericPromise_Resolve_ClearsFinallyHandlers()
        {
            WeakReference weakReference = null;

            IPromise<string> promise = new Promise<string>();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Finally(() =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve("aaa");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void GenericPromise_Resolve_ClearsProgressHandlers()
        {
            WeakReference weakReference = null;

            IPromise<string> promise = new Promise<string>();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.Progress((progress) =>
                {
                    Console.WriteLine(obj);
                });
            })();

            promise.Resolve("aaa");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }

        [TestMethod]
        public void GenericPromise_Resolve_ClearsCancelRequestedHandlers()
        {
            WeakReference weakReference = null;

            IPromise<string> promise = new Promise<string>();

            new Action(() => {
                var obj = new object();
                weakReference = new WeakReference(obj);

                promise.CancelRequested += (sender, e) =>
                {
                    Console.WriteLine(obj);
                };
            })();

            promise.Resolve("aaa");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.IsAlive, "promise is still holding a reference to a handler");
        }
        #endregion

        #region Promise<T>.StateChangedHandler
        [TestMethod]
        public void GenericPromise_StateChangedHandler_Invoked_WhenResolved()
        {
            var promise = new Promise<float>();
            var didBeforeTransition = false;

            PromiseStateChangedHandler callback = null;
            callback = (sender, e) =>
            {
                Assert.IsNotNull(e.Promise, "Promise is null");

                if (!didBeforeTransition)
                {
                    Assert.AreEqual(PromiseTransistionState.ResolvedBeforeCallbacks, e.PromiseTransistionState, "Expected ResolvedBeforeCallbacks transition type");
                    didBeforeTransition = true;
                }
                else
                {
                    Assert.AreEqual(PromiseTransistionState.ResolvedAfterCallbacks, e.PromiseTransistionState, "Expected ResolvedAfterCallbacks transition type");
                    Promise.PromiseStateChanged -= callback;
                }
            };

            Promise.PromiseStateChanged += callback;

            promise.Resolve(0);
        }

        [TestMethod]
        public void GenericPromise_StateChangedHandler_Invoked_WhenFailed()
        {
            var promise = new Promise<float>();
            var didBeforeTransition = false;

            PromiseStateChangedHandler callback = null;
            callback = (sender, e) =>
            {
                Assert.IsNotNull(e.Promise, "Promise is null");

                if (!didBeforeTransition)
                {
                    Assert.AreEqual(PromiseTransistionState.FailedBeforeCallbacks, e.PromiseTransistionState, "Expected FailedBeforeCallbacks transition type");
                    didBeforeTransition = true;
                }
                else
                {
                    Assert.AreEqual(PromiseTransistionState.FailedAfterCallbacks, e.PromiseTransistionState, "Expected FailedAfterCallbacks transition type");
                    Promise.PromiseStateChanged -= callback;
                }
            };

            Promise.PromiseStateChanged += callback;
            promise.Fail(new Exception());
        }

        [TestMethod]
        public void GenericPromise_StateChangedHandler_Invoked_WhenInitialized()
        {
            PromiseStateChangedHandler callback = null;
            callback = (sender, e) =>
            {
                Assert.IsNotNull(e.Promise, "Promise is null");
                Assert.AreEqual(PromiseTransistionState.Initialized, e.PromiseTransistionState);

                Promise.PromiseStateChanged -= callback;
            };

            var promise = new Promise<float>();
        }
        #endregion
        #endregion

        #region Test Fixtures
        private IPromise<bool> GetBoolFailedPromise()
        {
            return GetBoolFailedPromise(new Exception());
        }

        private IPromise<bool> GetBoolFailedPromise(Exception e)
        {
            IPromise<bool> promise = new Promise<bool>();

            promise.Fail(e);

            return promise;
        }

        private IPromise<bool> GetBoolPromise()
        {
            IPromise<bool> promise = new Promise<bool>();
            return promise;
        }

        private IPromise<bool> GetBoolResolvedPromise()
        {
            return GetBoolResolvedPromise(true);
        }

        private IPromise<bool> GetBoolResolvedPromise(bool resolvedValue = true)
        {
            IPromise<bool> promise = new Promise<bool>();

            promise.Resolve(resolvedValue);

            return promise;
        }

        private IPromise GetFailedCatchingPromise(Exception e, Action<Exception> catchCallback)
        {
            return GetFailedPromise(e)
                .Catch(catchCallback);
        }

        private IPromise GetFailedCatchingPromise(Exception e, Action<ArgumentException> catchCallback)
        {
            return GetFailedPromise(e)
                .Catch(catchCallback);
        }

        private IPromise GetFailedPromise()
        {
            IPromise promise = new Promise();
            promise.Fail(new Exception());
            return promise;
        }

        private IPromise GetFailedPromise(Exception e)
        {
            IPromise promise = new Promise();
            promise.Fail(e);
            return promise;
        }

        private IPromise<T> GetFailedGenericPromise<T>(Exception e)
        {
            IPromise<T> promise = new Promise<T>();
            promise.Fail(e);
            return promise;
        }

        private IPromise<int> GetIntPromise()
        {
            IPromise<int> promise = new Promise<int>();
            return promise;
        }

        private IPromise<int> GetIntResolvedPromise(int value)
        {
            IPromise<int> promise = new Promise<int>();

            promise.Resolve(value);

            return promise;
        }

        private IPromise<int> GetIntResolvedPromise()
        {
            IPromise<int> promise = new Promise<int>();

            promise.Resolve(1);

            return promise;
        }

        private IPromise GetPromise()
        {
            IPromise promise = new Promise();
            return promise;
        }

        private IPromise GetResolvedPromise()
        {
            IPromise promise = new Promise();
            promise.Resolve();
            return promise;
        }
        #endregion
    }
}