using ConsoleOps.Application.Abstractions.Messaging;

namespace ConsoleOps.Application.Features.Authentication;

/// <summary>
/// Why a sign-in did not complete.
/// </summary>
/// <remarks>
/// None of these repeat what GitHub said about a credential, and none say whether a login exists. A refusal that
/// distinguishes "not an operator here" from "no such account" tells an unwanted caller which half to work on.
/// </remarks>
internal static class AuthenticationErrors
{
    internal static readonly Error NotConfigured = new(
        "Auth.NotConfigured",
        "GitHub sign-in is not configured on this Console Ops.",
        ErrorType.Failure);

    internal static readonly Error StateMismatch = new(
        "Auth.StateMismatch",
        "That sign-in could not be verified. Start again from Console Ops rather than from a saved link.",
        ErrorType.Validation);

    internal static readonly Error CodeRejected = new(
        "Auth.CodeRejected",
        "GitHub did not accept that authorization. Start the sign-in again.",
        ErrorType.Validation);

    internal static readonly Error NotAnOperator = new(
        "Auth.NotAnOperator",
        "That GitHub account is not an operator of this Console Ops.",
        ErrorType.Forbidden);

    internal static readonly Error NoOperatorsConfigured = new(
        "Auth.NoOperatorsConfigured",
        "This Console Ops has no operators configured, so nobody can sign in. Set the allowed GitHub logins "
        + "before exposing it.",
        ErrorType.Forbidden);

    internal static readonly Error ProviderUnavailable = new(
        "Auth.ProviderUnavailable",
        "GitHub could not be reached to complete the sign-in.",
        ErrorType.Failure);

    internal static readonly Error NoSession = new(
        "Auth.NoSession",
        "No operator is signed in.",
        ErrorType.Forbidden);
}
