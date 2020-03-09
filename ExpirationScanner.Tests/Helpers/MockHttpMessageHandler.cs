using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Tests.Helpers
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _process;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_process(request));
        }
    }
}
