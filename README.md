# AwsLambdaOwin

[![NuGet](https://img.shields.io/nuget/v/AwsLambdaOwin.svg)](https://www.nuget.org/packages/AwsLambdaOwin/)
[![AppVeyor](https://img.shields.io/appveyor/ci/damianh/awslambdaowin.svg)](https://ci.appveyor.com/project/damianh/awslambdaowin)

This package makes it easy to run OWIN applications as a Lambda function with
API Gateway. This allows .NET core developers to create "serverless" applications
using OWIN and any framework that supports OWIN.

The function takes a request from an [API Gateway Proxy](0) and converts that
request into an OWIN enviroment dictionary and then converts the response from
your `AppFunc` into a response body that API Gateway Proxy understands.

**Note:** Lambda API Gateway Proxy to is a message orientated design. That is, it
expects request and response bodies to be strings (typically `application/json`)
and are buffered. It is not suitable for stream based APIs nor binary requests /
response.

## Example Lambda Function

In your OWIN application / library, add a class that extends
`APIGatewayOwinProxyFunction` and implement the Init method.

Here is an example implementation that uses [KatanaNetStandard](1) for the
`OwinContext` type.

```csharp
namespace TestOwinApp
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AwsLambdaOwin;
    using Microsoft.Owin;

    public class LambdaFunction : APIGatewayOwinProxyFunction
    {
        public LambdaFunction()
        {
            AppFunc = async env =>
            {
                var ctx = new OwinContext(env);
                await ctx.Response.WriteAsync("Hello OWIN on Lambda");
            }; 
        }

        public override Func<IDictionary<string, object>, Task> AppFunc { get; }
    }
}
```

There is also an `LambdaOwinContext` which is an extension of `OwinContext`
that give access to `APIGatewayProxyRequest` and `ILambdaContext`. This will
allow you to further adapt the owin enviroment to your needs.

```csharp
return async env =>
{
    var ctx = new LambdaOwinContext(env);
    var proxyRequest = ctx.ProxyRequest;
    var lambdaConext = ctx.LambdaConext;
    ...
};
```

The function handler for the Lambda function will be
`TestOwinApp::TestOwinApp.LambdaFunctionn::FunctionHandler`.

Once the function is deployed configure API Gateway with a HTTP Proxy to call
the Lambda Function. Refer to the API Gateway [developer guide][2] for more
information.

## Getting the API Gateway Request message

If the OWIN function is hosted in lambda, you can get the original API Gateway
Request message from the enviroment:

```csharp
protected override Func<IDictionary<string, object>, Task> Init()
{
    return async env =>
    {
        if (IsAPIGatewayProxyRequest(env))
        {
            var apiGatewayProxyRequest = GetAPIGatewayProxyRequest(env);
            ...
        }
        ...
    };
}
```

## Supporting Binary Response Content

The interface between the API Gateway and Lambda provides for and assumes
response content to be returned as a UTF-8 string. In order to return binary
content it is necessary to encode the raw response content in Base64 and to set
a flag in the response object that Base64-encoding has been applied.

In order to facilitate this mechanism, the `APIGatewayProxyFunction` base class
maintains a registry of MIME content types and how they should be transformed
before being returned to the calling API Gateway.  For any binary content types
that are returned by your application, you should register them for Base64
tranformation and then the framework will take care of intercepting any such
responses and making an necessary transformations to preserve the binary
content.  For example:

```csharp
namespace TestOwinApp
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AwsLambdaOwin;
    using Microsoft.Owin;

    public class LambdaFunction : APIGatewayProxyFunction
    {
        protected override Func<IDictionary<string, object>, Task> Init()
        {
             // Register any MIME content types you want treated as binary
            RegisterResponseContentEncodingForContentType(
                "application/octet-stream",
                ResponseContentEncoding.Base64);

            return async env =>
            {
                var binData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

                var ctx = new OwinContext(env);
                ctx.Response.ContentType = "application/octet-stream"
                await ctx.Response.WriteAsync(binData);
            };
        }
    }
}
```

### IMPORTANT - Registering Binary Response with API Gateway

In order to use this mechanism to return binary response content, in addition to
registering any binary MIME content types that will be returned by your
application, it also necessary to register those same content types with the API
Gateway using either the [console][5] or the [REST interface][6].

### Default Registered Content Types

By default several commonly used MIME types that are typically used with Web API services
are already pre-registered.  You can make use of these content types without any further
changes in your code, *however*, for any binary content types, you do still need to make
the necessary adjustments in the API Gateway as described above.


MIME Content Type | Response Content Encoding
------------------|--------------------------
`text/plain`               | Default (UTF-8)
`text/xml`                 | Default (UTF-8)
`application/xml`          | Default (UTF-8)
`application/json`         | Default (UTF-8)
`text/html`                | Default (UTF-8)
`text/css`                 | Default (UTF-8)
`text/javascript`          | Default (UTF-8)
`text/ecmascript`          | Default (UTF-8)
`text/markdown`            | Default (UTF-8)
`text/csv`                 | Default (UTF-8)
`application/octet-stream` | Base64
`image/png`                | Base64
`image/gif`                | Base64
`image/jpeg`               | Base64
`application/zip`          | Base64
`application/pdf`          | Base64


Content of readme adapted from [aws-lambda-dotnet][4].

Reach me at [randompunter][3] on twitter.

[0]: http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-create-api-as-simple-proxy.html
[1]: https://www.nuget.org/packages/KatanaNetStandard/
[2]: http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-create-api-as-simple-proxy.html
[3]: https://twitter.com/randompunter
[4]: https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.AspNetCoreServer
[5]: http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-console.html
[6]: http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-control-service-api.html
