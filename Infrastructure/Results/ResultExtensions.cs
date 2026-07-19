using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace OfferwallApi.Infrastructure.Results;

public static class ResultExtensions
{
    public static IResult ToHttpResponse(this Result result)
    {
        if (result.IsSuccess)
        {
            return HttpResults.Ok();
        }

        return MatchError(result.Error, result.Errors);
    }

    public static IResult ToHttpResponse<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return HttpResults.Ok(result.Value);
        }

        return MatchError(result.Error, result.Errors);
    }

    private static IResult MatchError(Error error, List<Error> errors)
    {
        return error.Type switch
        {
            ErrorType.Validation => HttpResults.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Validation Failed",
                Detail = "One or more validation errors occurred.",
                Extensions =
                {
                    ["errors"] = errors
                        .GroupBy(e => e.Code)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.Description).ToArray()
                        )
                }
            }),
            ErrorType.NotFound => HttpResults.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = error.Description
            }),
            ErrorType.Unauthorized => HttpResults.Unauthorized(),
            ErrorType.Forbidden => HttpResults.Forbid(),
            ErrorType.Conflict => HttpResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = error.Description
            }),
            _ => HttpResults.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}

