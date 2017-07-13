namespace Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using AwsLambdaOwin;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;
    using Microsoft.Owin;

    internal class Program
    {
        private static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<BenchmarkFunction>();
        }
    }

    public class BenchmarkFunction 
    {
        private readonly TestFunction _testFunction;
        private readonly OwinContext _owinContext;

        public BenchmarkFunction()
        {
            _testFunction = new TestFunction();
            _owinContext = new OwinContext();
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', 100000)));
            _owinContext.Response.Body = memoryStream;
        }

        [Benchmark]
        public void MarshalResponse()
        {
            _testFunction.MarshalResponse(_owinContext.Response);
        }
    }

    public class TestFunction : APIGatewayOwinProxyFunction
    {
        public TestFunction()
        {
            AppFunc = async env =>
            {
                var context = new OwinContext(env);
                await context.Response.WriteAsync(new string('a', 100000));
            };
        }
        
        public override Func<IDictionary<string, object>, Task> AppFunc { get; }

        public void MarshalResponse(IOwinResponse owinResponse)
        {
            base.MarshalResponse(owinResponse);
        }
    }
}