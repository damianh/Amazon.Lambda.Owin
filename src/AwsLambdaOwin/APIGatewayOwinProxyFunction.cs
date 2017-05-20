namespace AwsLambdaOwin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Amazon.Lambda.APIGatewayEvents;
    using Amazon.Lambda.Core;
    using Amazon.Lambda.Serialization.Json;
    using AwsLambdaOwin.Imports.Microsoft.IO.RecyclableMemoryStream;
    using Microsoft.Owin;
    using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

    public abstract class APIGatewayOwinProxyFunction
    {
        // Defines a mapping from registered content types to the response encoding format
        // which dictates what transformations should be applied before returning response content
        private readonly Dictionary<string, ResponseContentEncoding> _responseContentEncodingForContentType 
            = new Dictionary<string, ResponseContentEncoding>
        {
            // The complete list of registered MIME content-types can be found at:
            //    http://www.iana.org/assignments/media-types/media-types.xhtml

            // Here we just include a few commonly used content types found in
            // Web API responses and allow users to add more as needed below

            ["text/plain"] = ResponseContentEncoding.Default,
            ["text/xml"] = ResponseContentEncoding.Default,
            ["application/xml"] = ResponseContentEncoding.Default,
            ["application/json"] = ResponseContentEncoding.Default,
            ["text/html"] = ResponseContentEncoding.Default,
            ["text/css"] = ResponseContentEncoding.Default,
            ["text/javascript"] = ResponseContentEncoding.Default,
            ["text/ecmascript"] = ResponseContentEncoding.Default,
            ["text/markdown"] = ResponseContentEncoding.Default,
            ["text/csv"] = ResponseContentEncoding.Default,

            ["application/octet-stream"] = ResponseContentEncoding.Base64,
            ["image/png"] = ResponseContentEncoding.Base64,
            ["image/gif"] = ResponseContentEncoding.Base64,
            ["image/jpeg"] = ResponseContentEncoding.Base64,
            ["application/zip"] = ResponseContentEncoding.Base64,
            ["application/pdf"] = ResponseContentEncoding.Base64,
        };

        // Manage the serialization so the raw requests and responses can be logged.
        private readonly ILambdaSerializer _serializer = new JsonSerializer();
        private readonly RecyclableMemoryStreamManager _memoryStreamManager = new RecyclableMemoryStreamManager();

        /// <summary>
        ///     If true the request JSON coming from API Gateway will be logged. This is used to help debugging and not meant to be
        ///     enabled for production.
        /// </summary>
        public bool EnableRequestLogging { get; set; }

        /// <summary>
        ///     If true the response JSON coming sent to API Gateway will be logged. This is used to help debugging and not meant
        ///     to be enabled for production.
        /// </summary>
        public bool EnableResponseLogging { get; set; }

        /// <summary>
        /// Defines the default treatment of response content.
        /// </summary>
        public ResponseContentEncoding DefaultResponseContentEncoding { get; set; } = ResponseContentEncoding.Base64;

        public AppFunc AppFunc { get; }

        protected APIGatewayOwinProxyFunction()
        {
            AppFunc = Init();
        }

        protected abstract AppFunc Init();

        /// <summary>
        ///     The Lambda function handler that will be invoked vis APIGW.
        /// </summary>
        /// <param name="requestStream"></param>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        public virtual async Task<Stream> FunctionHandler(Stream requestStream, ILambdaContext lambdaContext)
        {
            if (EnableRequestLogging)
            {
                var reader = new StreamReader(requestStream);
                var json = reader.ReadToEnd();
                lambdaContext.Logger.LogLine(json);
                requestStream.Position = 0;
            }

            var proxyRequest = _serializer.Deserialize<APIGatewayProxyRequest>(requestStream);
            lambdaContext.Logger.LogLine($"Incoming {proxyRequest.HttpMethod} requests to {proxyRequest.Path}");

            var owinContext = new OwinContext();
            MarshalRequest(owinContext, proxyRequest);

            APIGatewayProxyResponseWithBase64Flag response;
            try
            {
                await AppFunc(owinContext.Environment);
                response = MarshalResponse(owinContext.Response);
            }
            catch (Exception ex)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(
                    $"Exception handling {proxyRequest.Path}");
                stringBuilder.AppendLine(ex.ToString());
                lambdaContext.Logger.Log(stringBuilder.ToString());

                throw;
            }

            var responseStream = new MemoryStream();
            _serializer.Serialize(response, responseStream);
            responseStream.Position = 0;

            if (EnableResponseLogging)
            {
                var reader = new StreamReader(responseStream);
                var json = reader.ReadToEnd();
                lambdaContext.Logger.LogLine(json);
                responseStream.Position = 0;
            }

            return responseStream;
        }

        /// <summary>
        ///     Registers a mapping from a MIME content type to a <see cref="ResponseContentEncoding"/>.
        /// </summary>
        /// <remarks>
        ///     The mappings in combination with the <see cref="DefaultResponseContentEncoding"/>
        ///     setting will dictate if and how response content should be transformed before being
        ///     returned to the calling API Gateway instance.
        /// <para>
        ///     The interface between the API Gateway and Lambda provides for repsonse content to
        ///     be returned as a UTF-8 string.  In order to return binary content without incurring
        ///     any loss or corruption due to transformations to the UTF-8 encoding, it is necessary
        ///     to encode the raw response content in Base64 and to annotate the response that it is
        ///     Base64-encoded.
        /// </para>
        /// <para>
        ///     <b>NOTE:</b>  In order to use this mechanism to return binary response content, in
        ///     addition to registering here any binary MIME content types that will be returned by
        ///     your application, it also necessary to register those same content types with the API
        ///     Gateway using either the <see
        ///     cref="http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-console.html"
        ///     >console</see> or the <see
        ///     cref="http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-control-service-api.html"
        ///     >REST interface</see>.
        /// </para>
        /// </remarks>
        public void RegisterResponseContentEncodingForContentType(string contentType, ResponseContentEncoding encoding)
        {
            _responseContentEncodingForContentType[contentType] = encoding;
        }


        /// <summary>
        ///     Populates the OwinContext with values from the proxy request.
        /// </summary>
        /// <param name="owinContext"></param>
        /// <param name="proxyRequest"></param>
        protected virtual void MarshalRequest(OwinContext owinContext, APIGatewayProxyRequest proxyRequest)
        {
            // The scheme is not available on the proxy request. If needed, it should be transported over custom header
            // and this MarshalRequest overridden.
            owinContext.Request.Scheme = "http"; 
            owinContext.Request.Method = proxyRequest.HttpMethod;
            owinContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(proxyRequest.Body ?? string.Empty));
            if (proxyRequest.Headers != null)
            {
                foreach (var header in proxyRequest.Headers)
                {
                    owinContext.Request.Headers.AppendCommaSeparatedValues(header.Key, header.Value.Split(','));
                }
            }

            owinContext.Request.Path = new PathString(proxyRequest.Path);
            
            if(proxyRequest.QueryStringParameters != null){

                var sb = new StringBuilder();
                var encoder = UrlEncoder.Default;
                foreach (var kvp in proxyRequest.QueryStringParameters)
                {
                    if (sb.Length > 1)
                    {
                        sb.Append("&");
                    }
                    sb.Append($"{encoder.Encode(kvp.Key)}={encoder.Encode(kvp.Value)}");
                }
                owinContext.Request.QueryString = new QueryString(sb.ToString());
            }

            owinContext.Response.Body = new MemoryStream();
        }

        /// <summary>
        /// Convert the response coming from ASP.NET Core into APIGatewayProxyResponse which is
        /// serialized into the JSON object that API Gateway expects.
        /// </summary>
        /// <param name="owinResponse"></param>
        /// <param name="statusCodeIfNotSet"></param>
        /// <returns><see cref="APIGatewayProxyResponseWithBase64Flag"/></returns>
        protected APIGatewayProxyResponseWithBase64Flag MarshalResponse(IOwinResponse owinResponse, int statusCodeIfNotSet = 200)
        {
            var response = new APIGatewayProxyResponseWithBase64Flag
            {
                StatusCode = owinResponse.StatusCode != 0 ? owinResponse.StatusCode : statusCodeIfNotSet
            };

            string contentType = null;
            response.Headers = new Dictionary<string, string>();

            foreach (var owinResponseHeader in owinResponse.Headers)
            {
                if (owinResponseHeader.Value.Length == 1)
                {
                    response.Headers[owinResponseHeader.Key] = owinResponseHeader.Value[0];
                }
                else
                {
                    response.Headers[owinResponseHeader.Key] = string.Join(",", owinResponseHeader.Value);
                }
                if (owinResponseHeader.Key.Equals("Content-Type", StringComparison.CurrentCultureIgnoreCase))
                {
                    contentType = response.Headers[owinResponseHeader.Key];
                }
            }

            if (owinResponse.Body != null)
            {
                var rcEncoding = DefaultResponseContentEncoding;
                if (contentType != null && _responseContentEncodingForContentType.ContainsKey(contentType))
                {
                    rcEncoding = _responseContentEncodingForContentType[contentType];
                }

                if (rcEncoding == ResponseContentEncoding.Base64)
                {
                    var responseBody = owinResponse.Body as MemoryStream;
                    if (responseBody != null)
                    {
                        response.Body = responseBody.TryGetBuffer(out var buffer)
                            ? Convert.ToBase64String(buffer.Array, buffer.Offset, buffer.Count)
                            : Convert.ToBase64String(responseBody.ToArray());
                    }
                    else
                    {
                        using (var ms = _memoryStreamManager.GetStream())
                        {
                            owinResponse.Body.CopyTo(ms);
                            response.Body = ms.TryGetBuffer(out var buffer)
                                ? Convert.ToBase64String(buffer.Array, buffer.Offset, buffer.Count)
                                : Convert.ToBase64String(ms.ToArray());
                        }
                    }
                    response.IsBase64Encoded = true;
                }
                else
                {
                    var body = owinResponse.Body as MemoryStream;
                    if (body != null)
                    {
                        response.Body = body.TryGetBuffer(out var buffer)
                            ? Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count)
                            : Encoding.UTF8.GetString(body.ToArray());
                    }
                    else
                    {
                        owinResponse.Body.Position = 0;
                        using (var reader = new StreamReader(owinResponse.Body, Encoding.UTF8))
                        {
                            response.Body = reader.ReadToEnd();
                        }
                    }
                }
            }

            return response;
        }
    }
}