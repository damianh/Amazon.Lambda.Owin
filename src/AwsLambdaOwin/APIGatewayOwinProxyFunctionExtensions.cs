// ReSharper disable once CheckNamespace
namespace Amazon.Lambda.TestUtilities
{
    using System.IO;
    using System.Threading.Tasks;
    using Amazon.Lambda.APIGatewayEvents;
    using Amazon.Lambda.Core;
    using Amazon.Lambda.Serialization.Json;
    using AwsLambdaOwin;

    public static class ApiGatewayOwinProxyFunctionExtensions
    {
        public static async Task<APIGatewayProxyResponse> FunctionHandler(
            this APIGatewayOwinProxyFunction function,
            APIGatewayProxyRequest request,
            ILambdaContext lambdaContext)
        {
            ILambdaSerializer serializer = new JsonSerializer();

            var requestStream = new MemoryStream();
            serializer.Serialize(request, requestStream);
            requestStream.Position = 0;

            var responseStream = await function.FunctionHandler(requestStream, lambdaContext);

            var response = serializer.Deserialize<APIGatewayProxyResponse>(responseStream);
            return response;
        }
    }
}