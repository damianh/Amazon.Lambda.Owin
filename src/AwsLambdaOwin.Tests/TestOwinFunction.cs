namespace AwsLambdaOwin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Owin;

    public class TestOwinFunction : APIGatewayOwinProxyFunction
    {
        public OwinContext LastRequest;

        public TestOwinFunction()
        {
            EnableRequestLogging = GetEnvironmentVariableBool("EnableRequestLogging");
            EnableResponseLogging = GetEnvironmentVariableBool("EnableResponseLogging");
        }

        protected override Func<IDictionary<string, object>, Task> Init()
        {
            return async env =>
            {
                var context = new LambdaOwinContext(env);

                if(context.Request.Path.StartsWithSegments(PathString.FromUriComponent("/img")))
                {
                    var stream = typeof(TestOwinFunction).GetTypeInfo().Assembly.GetManifestResourceStream("AwsLambdaOwin.doge.jpg");
                    var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    context.Response.ContentType = "image/jpeg";
                    context.Response.ContentLength = stream.Length;
                    context.Response.Body = memoryStream;
                    return;
                }

                if (context.Request.Path.StartsWithSegments(PathString.FromUriComponent("/img_nomemorystream")))
                {
                    var stream = typeof(TestOwinFunction).GetTypeInfo().Assembly.GetManifestResourceStream("AwsLambdaOwin.doge.jpg");
                    context.Response.ContentType = "image/jpeg";
                    context.Response.ContentLength = stream.Length;
                    context.Response.Body = stream;
                    return;
                }

                if (context.Request.Path.StartsWithSegments(PathString.FromUriComponent("/text")))
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes("foo"));
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength = stream.Length;
                    context.Response.Body = new StreamWrapper(stream);

                    return;
                }
                context.Response.StatusCode = 202;
                context.Response.ReasonPhrase = "OK";
                context.Response.ContentType = "text/plain";
                if (context.ProxyRequest != null)
                {
                    await context.Response.WriteAsync(context.ProxyRequest.RequestContext.RequestId);
                }
                else
                {
                    await context.Response.WriteAsync("Inside Lambda");
                }
                LastRequest = context;
            };
        }

        private static bool GetEnvironmentVariableBool(string name)
        {
            var variable = Environment.GetEnvironmentVariable("EnableRequestLogging");
            bool.TryParse(variable, out bool result);
            return result;
        }

        internal class StreamWrapper : Stream
        {
            private readonly Stream _inner;

            public StreamWrapper(Stream inner)
            {
                _inner = inner;
            }

            public override void Flush()
            {
                _inner.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _inner.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _inner.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
            }

            public override bool CanRead => _inner.CanRead;

            public override bool CanSeek => _inner.CanSeek;

            public override bool CanWrite => _inner.CanWrite;

            public override long Length => _inner.Length;

            public override long Position
            {
                get => _inner.Position;
                set => _inner.Position = value;
            }
        }
    }
}