using Akka.Actor;
using Akka.net.Actors.Messages;

namespace Akka.net.Actors
{
    public class WaitBroker<TRequest, TResponse> : ReceiveActor
    {
        public static Props CreateProps()
        {
            return Props.Create<WaitBroker<TRequest, TResponse>>();
        }

        public WaitBroker()
        {
            Ready();
        }

        private void Ready()
        {
            Receive<WaitForProcessMessage<TRequest>>(request =>
            {
                var taskCompletionSource = new System.Threading.Tasks.TaskCompletionSource<TResponse>();
                var waitForwarderActorRef = Context.System.ActorOf(WaitForwarder<TRequest, TResponse>.CreateProps(taskCompletionSource));
                Sender.Tell(taskCompletionSource.Task);
                waitForwarderActorRef.Tell(request);
            });
        }
    }
}