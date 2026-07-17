using Planura.Shared.Errors.Models;
using Planura.Shared.Errors.Response;
using System.Net;

namespace Planura.Apis.MiddleWares
{
 
        public class ExeptionHandlerMiddleware
        {
            private readonly RequestDelegate _next;
            private readonly ILogger<ExeptionHandlerMiddleware> _logger;
            private readonly IHostEnvironment _env;
            public ExeptionHandlerMiddleware(RequestDelegate next, ILogger<ExeptionHandlerMiddleware> loggerFactory, IHostEnvironment env)
            {
                _next = next;
                _logger = loggerFactory;
                _env = env;
            }

            public async Task InvokeAsync(HttpContext httpContext)
            {
                try
                {
                    await _next(httpContext);

                    if (httpContext.Response.StatusCode == (int)HttpStatusCode.MethodNotAllowed)
                    {
                        httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        httpContext.Response.ContentType = "application/json";
                        var respnse = new ApiResponse((int)HttpStatusCode.Unauthorized, $"You Are Not Authorized");
                        await httpContext.Response.WriteAsync(respnse.ToString());
                    }
                }
                catch (Exception ex)
                {

                    if (_env.IsDevelopment())
                    {
                        _logger.LogError(ex, ex.Message);
                    }
                    else
                    {
                        // production mode
                        // log exeption details t (file | text)

                    }
                    await HandleExceptionAsync(httpContext, ex);

                }
            }

            private async Task HandleExceptionAsync(HttpContext httpContext, Exception ex)
            {
                ApiResponse response;
                switch (ex)
                {
                    case NotFoundExeption:
                        httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse((int)HttpStatusCode.NotFound, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());
                        break;

                    case SlotUnavailableExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse((int)HttpStatusCode.Conflict, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case ValidationExeption validationExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiValidationErrorResponse(ex.Message) { Erroes = validationExeption.Errors };
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case BadRequestExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse(400, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case PaymentDeclinedExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse((int)HttpStatusCode.PaymentRequired, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case UnAuthorizedExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse(401, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case AiProviderRateLimitedExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse((int)HttpStatusCode.TooManyRequests, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case AiProviderTimeoutExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse((int)HttpStatusCode.GatewayTimeout, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case AiContentPolicyExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse((int)HttpStatusCode.BadRequest, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    case AiProviderUnavailableExeption:

                        httpContext.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                        httpContext.Response.ContentType = "application/json";
                        response = new ApiResponse((int)HttpStatusCode.BadGateway, ex.Message);
                        await httpContext.Response.WriteAsync(response.ToString());

                        break;
                    default:
                        var innerMessage = ex;
                        while (innerMessage.InnerException is not null)
                            innerMessage = innerMessage.InnerException;

                        response = _env.IsDevelopment()
                            ? new ApiExeptionResponse(
                                (int)HttpStatusCode.InternalServerError,
                                ex.Message,
                                $"{innerMessage.Message}{Environment.NewLine}{ex.StackTrace?.ToString()}")
                            : new ApiExeptionResponse((int)HttpStatusCode.InternalServerError);



                        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        httpContext.Response.ContentType = "application/json";

                        await httpContext.Response.WriteAsync(response.ToString());
                        break;


                }

            }
        }
    }

