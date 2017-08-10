using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.net.Actors.Messages;

namespace Akka.net.Actors
{
    internal class WaitForwarder<TRequest, TResponse> : ReceiveActor
    {
        public static Props CreateProps(TaskCompletionSource<TResponse> taskCompletionSource)
        {
            return Props.Create<WaitForwarder<TRequest, TResponse>>(taskCompletionSource);
        }

        private readonly TaskCompletionSource<TResponse> _taskCompletionSource;

        public WaitForwarder(TaskCompletionSource<TResponse> taskCompletionSource)
        {
            _taskCompletionSource = taskCompletionSource;
            Ready();
        }

        private void Ready()
        {
            ReceiveAsync<WaitForProcessMessage<TRequest>>(async request =>
            {
                try
                {
                    Become(WaitingForResponse);
                    var response = await request.ProcessorActorRef.Ask<TResponse>(request.Message, request.WaitTimeout);
                    _taskCompletionSource.SetResult(response);
                }
                catch (Exception ex)
                {
                    _taskCompletionSource.SetException(ex);
                }
                await Self.GracefulStop(TimeSpan.Zero);
            });
        }

        private void WaitingForResponse()
        {
            // TODO cancel?
            ReceiveAny(anything =>
            {
                // ignore
            });
        }
    }
}
