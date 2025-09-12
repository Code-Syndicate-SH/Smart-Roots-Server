using Microsoft.AspNetCore.Diagnostics;

namespace Smart_Roots_Server.Exceptions {
    public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
            public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
                Exception exception,
                CancellationToken cancellationToken) {
                logger.LogError(exception, "Unhandled Exception occured.");
                // this will have context of the req 
                httpContext.Response.StatusCode = exception switch {
                    ApplicationException => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError,
                }; // if it was in the app we throw 400 else we throw a server error

                return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext {
                    HttpContext = httpContext,
                    Exception = exception,
                    ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails {
                        Type = exception.GetType().Name,
                        Title = "An error occured.",
                        Detail = exception.Message,
                    }
                });
            }


        }
    }


