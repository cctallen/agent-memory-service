using AgentMemoryService.Shared.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentMemoryService.Shared.Infrastructure;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess != null ? onSuccess(result.Value!) : Results.Ok(result.Value);

        var problem = new ProblemDetails
        {
            Title = result.Error!.Code,
            Detail = result.Error.Message,
            Status = result.Error.Type switch
            {
                ErrorType.NotFound    => 404,
                ErrorType.Validation  => 400,
                ErrorType.Conflict    => 409,
                ErrorType.Forbidden   => 403,
                ErrorType.Unauthorized => 401,
                _                     => 500,
            }
        };

        return result.Error!.Type switch
        {
            ErrorType.NotFound     => Results.NotFound(problem),
            ErrorType.Validation   => Results.BadRequest(problem),
            ErrorType.Conflict     => Results.Conflict(problem),
            ErrorType.Forbidden    => Results.Json(problem, statusCode: 403),
            ErrorType.Unauthorized => Results.Json(problem, statusCode: 401),
            _                      => Results.Problem(result.Error.Message, statusCode: 500, title: result.Error.Code)
        };
    }
}
