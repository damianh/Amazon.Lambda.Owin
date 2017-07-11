using System.Collections.Generic;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Owin;

namespace AwsLambdaOwin
{
    public class LambdaOwinContext : OwinContext
    {
        private readonly IDictionary<string, object> _environment;

        public LambdaOwinContext(IDictionary<string, object> environment)
            : base(environment)
        {
            _environment = environment;
        }

        public APIGatewayProxyRequest ProxyRequest
        {
            get
            {
                if (!_environment.ContainsKey(APIGatewayOwinProxyFunction.APIGatewayProxyRequestKey))
                {
                    return null;
                }
                return (APIGatewayProxyRequest)_environment[APIGatewayOwinProxyFunction.APIGatewayProxyRequestKey];
            }
        }

        public ILambdaContext LambdaContext
        {
            get
            {
                if (!_environment.ContainsKey(APIGatewayOwinProxyFunction.LambdaContextKey))
                {
                    return null;
                }
                return (ILambdaContext)_environment[APIGatewayOwinProxyFunction.LambdaContextKey];
            }
        }
    }
}