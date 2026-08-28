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

        return result.Error!.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new ProblemDetails { Title = result.Error.Code, Detail = result.Error.Message, Status = 404 }),
            ErrorType.Validation => Results.BadRequest(new ProblemDetails { Title = result.Error.Code, Detail = result.Error.Message, Status = 400 }),
            ErrorType.Conflict => Results.Conflict(new ProblemDetails { Title = result.Error.Code, Detail = result.Error.Message, Status = 409 }),
            ErrorType.Forbidden => Results.Forbid(),
            ErrorType.Unauthorized => Results.Unauthorized(),
            _ => Results.Problem(result.Error.Message, statusCode: 500, title: result.Error.Code)
        };
    }
}
