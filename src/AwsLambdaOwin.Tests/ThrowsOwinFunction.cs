namespace AwsLambdaOwin
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ThrowsOwinFunction : APIGatewayOwinProxyFunction
    {
        public ThrowsOwinFunction()
        {
            AppFunc = async env => throw new Exception("boom");
        }
        
        public override Func<IDictionary<string, object>, Task> AppFunc { get; }
    }
}