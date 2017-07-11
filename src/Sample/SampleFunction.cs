namespace Sample
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using AwsLambdaOwin;
    using Microsoft.Owin;

    public class SampleFunction : APIGatewayOwinProxyFunction
    {
        protected override Func<IDictionary<string, object>, Task> Init()
        {
            return async env =>
            {
                var context = new LambdaOwinContext(env);
                if (context.Request.Path.StartsWithSegments(PathString.FromUriComponent("/img")))
                {
                    var stream = typeof(SampleFunction).GetTypeInfo().Assembly.GetManifestResourceStream("Sample.doge.jpg");
                    context.Response.ContentType = "image/jpeg";
                    context.Response.ContentLength = stream.Length;
                    context.Response.Body = stream;
                    return;
                }

                context.Response.Headers.Append("Content-Type", "text/plain");

                var proxyRequest = context.ProxyRequest;

                await context.Response.WriteAsync(proxyRequest.RequestContext.RequestId);
            };
        }
    }
}