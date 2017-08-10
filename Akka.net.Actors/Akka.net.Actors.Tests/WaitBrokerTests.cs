using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.net.Actors.Messages;
using Akka.Util.Internal;
using NUnit.Framework;

namespace Akka.net.Actors.Tests
{
    public class WaitBrokerTests : TestKit.NUnit.TestKit
    {
        [Test]
        public async void WaitForProcessMessageSuccessfulTest()
        {
            // Arrange
            var processorTestActorRef = CreateTestProbe("processor");
            
            var waitBroker = Sys.ActorOf(WaitBroker<int, string>.CreateProps());
            var waitForProcessMessage =
                new WaitForProcessMessage<int>(processorTestActorRef, 777, TimeSpan.FromMilliseconds(5000));

            // Act 
            var taskToWait = await waitBroker.Ask<Task<string>>(waitForProcessMessage);

            // Assert
            await Task.Delay(500); // wait 500ms
            // task should be still running as processor actor has not replied or timed out
            Assert.IsFalse(taskToWait.IsCanceled); 
            Assert.IsFalse(taskToWait.IsCompleted);
            Assert.IsFalse(taskToWait.IsFaulted);

            Assert.IsTrue(processorTestActorRef.HasMessages);
            
            // simulate processing has finished
            var messageToProcessReceived = processorTestActorRef.ExpectMsg<int>();
            Assert.AreEqual(777, messageToProcessReceived);
            processorTestActorRef.Reply("hello");
            await Task.Delay(500);
            AwaitAssert(() => Assert.AreEqual("hello", taskToWait.Result), TimeSpan.FromMilliseconds(500));
        }

        [Test]
        public async void WaitForProcessMessageTimeoutTest()
        {
            // Arrange
            var processorTestActorRef = CreateTestProbe("processor");

            var waitBroker = Sys.ActorOf(WaitBroker<int, string>.CreateProps());
            var waitForProcessMessage =
                new WaitForProcessMessage<int>(processorTestActorRef, 777, TimeSpan.FromMilliseconds(600));

            // Act 
            var taskToWait = await waitBroker.Ask<Task<string>>(waitForProcessMessage);

            // Assert
            await Task.Delay(500); // wait 500ms
            // task should be still running as processor actor has not replied or timed out
            Assert.IsFalse(taskToWait.IsCanceled);
            Assert.IsFalse(taskToWait.IsCompleted);
            Assert.IsFalse(taskToWait.IsFaulted);

            Assert.IsTrue(processorTestActorRef.HasMessages);

            // timeout
            await Task.Delay(300);
            AwaitAssert(() => Assert.IsTrue(taskToWait.IsFaulted), TimeSpan.FromMilliseconds(500));
        }


        [TestCase(10)]
        [TestCase(1000)]
        public async void WaitForProcessMessageMultipleSuccessfulTest(int processorsCount)
        {
            // Arrange
            var range = Enumerable.Range(0, processorsCount).ToArray();
            var processorTestActorRefs = range
                .Select(i => new {actorRef = CreateTestProbe($"processor{i}"), message = i}).ToArray();

            var waitBroker = Sys.ActorOf(WaitBroker<int, string>.CreateProps());
            var waitForProcessMessagesAndReplies = processorTestActorRefs.Select(ptar =>
                new
                {
                    replyMessage = $"{ptar.message}",
                    waitForProcessMessage =
                    new WaitForProcessMessage<int>(ptar.actorRef, ptar.message, TimeSpan.FromMilliseconds(5000))
                }).ToArray();

            // Act 
            var tasksToWaitAndReplies = await Task.WhenAll(waitForProcessMessagesAndReplies
                .Select(async wfpmar => new
                {
                    taskToWait = await waitBroker.Ask<Task<string>>(wfpmar.waitForProcessMessage),
                    wfpmar.replyMessage
                }).ToArray());

            // Assert
            Thread.Sleep(500); // wait 500ms
            // task should be still running as processor actor has not replied or timed out
            Assert.IsFalse(tasksToWaitAndReplies.Select(ttwar => ttwar.taskToWait.IsCanceled)
                .Aggregate((t1IsCanceled, t2IsCanceled) => t1IsCanceled || t2IsCanceled));
            Assert.IsFalse(tasksToWaitAndReplies.Select(ttwar => ttwar.taskToWait.IsCompleted)
                .Aggregate((t1IsCompleted, t2IsCompleted) => t1IsCompleted || t2IsCompleted));
            Assert.IsFalse(tasksToWaitAndReplies.Select(ttwar => ttwar.taskToWait.IsFaulted)
                .Aggregate((t1IsFaulted, t2IsFaulted) => t1IsFaulted || t2IsFaulted));

            Assert.IsTrue(processorTestActorRefs.Select(ptar => ptar.actorRef.HasMessages)
                .Aggregate((ptar1HasMessages, ptar2HasMessages) => ptar1HasMessages && ptar2HasMessages));

            // simulate processing has finished
            var messagesToProcessReceived = processorTestActorRefs
                .Select(ptar => new {expected = ptar.message, received = ptar.actorRef.ExpectMsg<int>()}).ToArray();
            messagesToProcessReceived.ForEach(mtpr => Assert.AreEqual(mtpr.expected, mtpr.received));

            processorTestActorRefs.ForEach(ptar => ptar.actorRef.Reply($"{ptar.message}"));
            Thread.Sleep(1000);
            tasksToWaitAndReplies.ForEach(ttwar =>
                AwaitAssert(() => Assert.AreEqual($"{ttwar.replyMessage}", ttwar.taskToWait.Result),
                    TimeSpan.FromMilliseconds(500)));
        }
    }
}
