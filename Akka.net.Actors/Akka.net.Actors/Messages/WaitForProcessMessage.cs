using System;
using Akka.Actor;

namespace Akka.net.Actors.Messages
{
    public class WaitForProcessMessage<T>
    {
        public WaitForProcessMessage(IActorRef processorActorRef, T message, TimeSpan waitTimeout)
        {
            ProcessorActorRef = processorActorRef;
            Message = message;
            WaitTimeout = waitTimeout;
        }

        public IActorRef ProcessorActorRef { get; }
        public T Message { get; }
        public TimeSpan WaitTimeout { get; }
    }
}