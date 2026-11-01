using EventHive.Shared.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EventHive.Shared.Extensions;

public static class ResultExtensions
{

    public static IActionResult ToErrorResponse<T>(this Result<T> result, ControllerBase controller)
    {
        var primary = result.Errors.FirstOrDefault();

        if (primary is null)
            return controller.StatusCode(500, "An unexpected error occurred.");

        var descriptions = result.Errors.Select(e => e.Description).ToList();

        return primary.Type switch
        {
            ErrorType.NotFound     => controller.NotFound(descriptions),
            ErrorType.Validation   => controller.BadRequest(descriptions),
            ErrorType.Unauthorized => controller.Unauthorized(),
            ErrorType.Conflict           => controller.Conflict(descriptions),
            ErrorType.ServiceUnavailable => controller.StatusCode(StatusCodes.Status503ServiceUnavailable, descriptions),
            ErrorType.Failure            => controller.StatusCode(500, descriptions),
            _                            => controller.StatusCode(500, descriptions)
        };
    }
}
