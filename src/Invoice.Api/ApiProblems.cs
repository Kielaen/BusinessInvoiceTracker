using Microsoft.AspNetCore.Http.HttpResults;

namespace Invoice.Api;

public static class ApiProblems
{
    private const string ProblemBase =
        "https://business-invoice-tracker.invalid/problems/";

    public static ProblemHttpResult Unauthorized() => TypedResults.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Authentication required",
        type: $"{ProblemBase}authentication-required",
        detail: "Sign in and try the request again.");

    public static ProblemHttpResult Forbidden() => TypedResults.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Access denied",
        type: $"{ProblemBase}access-denied",
        detail: "Your account does not have permission to perform this action.");

    public static ProblemHttpResult NotFound() => TypedResults.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Resource not found",
        type: $"{ProblemBase}resource-not-found",
        detail: "The requested resource was not found or is not available to your account.");

    public static ProblemHttpResult TooManyRequests() => TypedResults.Problem(
        statusCode: StatusCodes.Status429TooManyRequests,
        title: "Too many requests",
        type: $"{ProblemBase}rate-limit-exceeded",
        detail: "Wait briefly before trying again.");

    public static ProblemHttpResult Conflict(string detail) => TypedResults.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "The request conflicts with current state",
        type: $"{ProblemBase}state-conflict",
        detail: detail);

    public static ValidationProblem Validation(
        IReadOnlyDictionary<string, string[]> errors,
        string title = "The request is invalid") =>
        TypedResults.ValidationProblem(
            errors,
            title: title,
            type: $"{ProblemBase}validation-failed");

    public static ValidationProblem InvalidLoginRequest(
        IReadOnlyDictionary<string, string[]> errors) =>
        Validation(errors, "The login request is invalid");
}
