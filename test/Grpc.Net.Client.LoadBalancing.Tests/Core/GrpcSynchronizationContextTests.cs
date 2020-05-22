#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcSynchronizationContextTests
    {
        [Fact]
        public void ForNullAction_UseGrpcSynchronizationContext_ThrowException()
        {
            // Arrange
            var context = new GrpcSynchronizationContext((exception) => { });

            // Act
            // Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.Throws<ArgumentNullException>(() => { context.ExecuteLater(null); });
            Assert.Throws<ArgumentNullException>(() => { context.Execute(null); });
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public void ForSingleActionExecuteOnSingleThread_UseGrpcSynchronizationContext_VerifyExecute()
        {
            // Arrange
            var wasExecuted = false;
            var context = new GrpcSynchronizationContext((exception) => { });
            var action = new Action(() => wasExecuted = true);

            // Act
            context.Execute(action);

            // Assert
            Assert.True(wasExecuted);
        }

        [Fact]
        public void ForSingleActionExecuteLaterOnSingleThread_UseGrpcSynchronizationContext_VerifyExecute()
        {
            // Arrange
            var wasExecuted = false;
            var context = new GrpcSynchronizationContext((exception) => { });
            var action = new Action(() => wasExecuted = true);

            // Act
            context.ExecuteLater(action);
            context.Drain();

            // Assert
            Assert.True(wasExecuted);
        }

        [Fact]
        public async Task ForActionsExecutedOnSingleThread_UseGrpcSynchronizationContext_VerifyExecuteInOrder()
        {
            // Arrange
            var errors = new List<Exception>();
            var results = new ConcurrentQueue<int>();
            var lockObject = new object();
            var context = new GrpcSynchronizationContext((exception) => { errors.Add(exception); });
            var action1 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(lockObject, 1, results));
            var action2 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(lockObject, 2, results));
            var action3 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(lockObject, 3, results));

            // Act
            var thread1 = new Thread(() =>
            {
                context.ExecuteLater(action1);
                context.ExecuteLater(action2);
                context.Drain();
                Assert.Contains(1, results);
                Assert.Contains(2, results);
                context.ExecuteLater(action3);
                context.Drain();
                Assert.Contains(3, results);
                context.Execute(action3);
                context.ExecuteLater(action1);
                context.ExecuteLater(action2);
                context.Drain();
            });
            ThreadsAllStarted(thread1);
            while (!ThreadsAllCompleted(thread1))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            // Assert
            Assert.Empty(errors);
            Assert.NotEmpty(results);
            Assert.Equal(new int[] { 1, 2, 3, 3, 1, 2 }, results.ToArray());
        }

        [Fact]
        public async Task ForActionsExecutedOnMultipleThreads_UseGrpcSynchronizationContext_VerifyExecuteSequentiallyUsingExternalMonitor()
        {
            // Arrange
            var errors = new List<Exception>();
            var results = new ConcurrentQueue<int>();
            var lockObject = new object();
            var context = new GrpcSynchronizationContext((exception) => { errors.Add(exception); });
            var action1 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(lockObject, 1, results));
            var action2 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(lockObject, 2, results));
            var action3 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(lockObject, 3, results));

            // Act
            var thread1 = new Thread(() => ScheduleWork(context, 50, new Action[] { action1, action2, action3 }, 0.5, 0.3));
            var thread2 = new Thread(() => ScheduleWork(context, 50, new Action[] { action1, action2, action3 }, 0.5, 0.5));
            var thread3 = new Thread(() => ScheduleWork(context, 20, new Action[] { action1, action2, action3 }, 0.2, 0.8));
            var thread4 = new Thread(() => ScheduleWork(context, 50, new Action[] { action1, action2, action3 }, 0.5, 0.5));
            ThreadsAllStarted(thread1, thread2, thread3, thread4);
            while (!ThreadsAllCompleted(thread1, thread2, thread3, thread4))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            // Assert
            // If case of concurrency TaskMethodThatVerifyIfConcurrencyOccurs will throw exception
            // context will store all exceptions to erros list
            Assert.Empty(errors); 
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task ForActionsExecutedOnMultipleThreads_UseGrpcSynchronizationContext_VerifyExecuteSequentiallyUsingContextMethod()
        {
            // Arrange
            var errors = new List<Exception>();
            var results = new ConcurrentQueue<int>();
            var lockObject = new object();
            var context = new GrpcSynchronizationContext((exception) => { errors.Add(exception); });
            var action1 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(context, 1, results));
            var action2 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(context, 2, results));
            var action3 = new Action(() => TaskMethodThatVerifyIfConcurrencyOccurs(context, 3, results));

            // Act
            var thread1 = new Thread(() => ScheduleWork(context, 50, new Action[] { action1, action2, action3 }, 0.5, 0.3));
            var thread2 = new Thread(() => ScheduleWork(context, 50, new Action[] { action1, action2, action3 }, 0.5, 0.5));
            var thread3 = new Thread(() => ScheduleWork(context, 20, new Action[] { action1, action2, action3 }, 0.2, 0.8));
            var thread4 = new Thread(() => ScheduleWork(context, 50, new Action[] { action1, action2, action3 }, 0.5, 0.5));
            ThreadsAllStarted(thread1, thread2, thread3, thread4);
            while (!ThreadsAllCompleted(thread1, thread2, thread3, thread4))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            // Assert
            // If case of concurrency TaskMethodThatVerifyIfConcurrencyOccurs will throw exception
            // context will store all exceptions to erros list
            Assert.Empty(errors);
            Assert.NotEmpty(results);
        }

        [Fact]
        public void ForActionThatThrowException_UseGrpcSynchronizationContext_VerifyExceptionIsHandedByContext()
        {
            // Arrange
            var errorMessage = $"message-error-{Guid.NewGuid().ToString().Substring(0, 4)}";
            var errors = new List<Exception>();
            var context = new GrpcSynchronizationContext((exception) => { errors.Add(exception); });
            var action = new Action(() => throw new Exception(errorMessage));

            // Act
            context.ExecuteLater(action);
            context.Execute(action); // will trigger drain

            // Assert
            Assert.NotEmpty(errors);
            Assert.Equal(2, errors.Count);
            Assert.Equal(errorMessage, errors[0].Message);
            Assert.Equal(errorMessage, errors[1].Message);
        }

        [Fact]
        public async Task ForSingleActionScheduledOnSingleThread_UseGrpcSynchronizationContext_VerifyExecute()
        {
            // Arrange
            var wasExecuted = false;
            var context = new GrpcSynchronizationContext((exception) => { });
            var action = new Action(() => wasExecuted = true);

            // Act
            var scheduledHandle = context.Schedule(action, TimeSpan.Zero);
            while (scheduledHandle.IsPending())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            // Assert
            Assert.True(wasExecuted);
            Assert.False(scheduledHandle.IsPending());
            scheduledHandle.Cancel(); // calling cancel on scheduled task that has already been executed does nothing
            Assert.False(scheduledHandle.IsPending());
        }

        [Fact]
        public void ForSingleActionScheduledAndCancelledOnSingleThread_UseGrpcSynchronizationContext_VerifyNotExecuted()
        {
            // Arrange
            var wasExecuted = false;
            var context = new GrpcSynchronizationContext((exception) => { });
            var action = new Action(() => wasExecuted = true);

            // Act
            var scheduledHandle = context.Schedule(action, TimeSpan.FromSeconds(5));
            Assert.True(scheduledHandle.IsPending());
            scheduledHandle.Cancel();
            Assert.False(scheduledHandle.IsPending());
            context.Drain(); // double verification that nothing was scheduled

            // Assert
            Assert.False(wasExecuted);
        }

        private static void ThreadsAllStarted(params Thread[] threads)
        {
            foreach (var thread in threads)
            {
                thread.Start();
            }
        }

        private static bool ThreadsAllCompleted(params Thread[] threads)
        {
            foreach (var thread in threads)
            {
                if (thread.IsAlive)
                {
                    return false;
                }
            }
            return true;
        }

        private static void ScheduleWork(GrpcSynchronizationContext context, int ammountOfWork, 
            IReadOnlyList<Action> tasks, double workScheduleProbability, double drainScheduleProbability)
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            for (int i = 0; i < ammountOfWork; i++)
            {
                if (random.NextDouble() < workScheduleProbability)
                {
                    context.ExecuteLater(tasks[random.Next(0, tasks.Count)]);
                }
                if (random.NextDouble() < drainScheduleProbability)
                {
                    context.Drain();
                }
            }
        }

        private static void TaskMethodThatVerifyIfConcurrencyOccurs(object lockObject, int value, ConcurrentQueue<int> results)
        {
            if (Monitor.TryEnter(lockObject)) // Request the lock.
            {
                try
                {
                    results.Enqueue(value); // Do some job.
                }
                finally
                {
                    Monitor.Exit(lockObject); // Ensure that the lock is released. 
                }
            }
            else
            {
                throw new InvalidOperationException("Another thread/task request lock"); // Concurrency observed. 
            }
        }

        private static void TaskMethodThatVerifyIfConcurrencyOccurs(GrpcSynchronizationContext context, int value, ConcurrentQueue<int> results)
        {
            context.ThrowIfNotInThisSynchronizationContext();
            results.Enqueue(value); // Do some job.
        }
    }
}
