using ConsoleOps.Api.Security;
using ConsoleOps.Application.Integrations.GitHub;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Api.Extensions;

public static class OperatorCredentialExtensions
{
    /// <summary>
    /// Makes GitHub reads act as the operator who made the request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registered here rather than in Infrastructure because only the API knows there is a request. Infrastructure
    /// registers the configured service token as the default, which is what scheduled collection keeps using; this
    /// replaces it with a credential that prefers the signed-in operator and falls back to that same default when
    /// there is no operator.
    /// </para>
    /// <para>
    /// Replaced rather than added, so resolving the port cannot depend on registration order.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddOperatorGitHubCredential(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Singleton<IGitHubCredential, OperatorGitHubCredential>());

        return services;
    }
}
