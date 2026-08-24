using Microsoft.OpenApi;

namespace ConsoleOps.Api.Extensions
{
    public static class SwaggerExtensions
    {
        public static IServiceCollection AddSwagger(this IServiceCollection service)
        {
            service.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("ConsoleOpskey", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Description = "Enter the value stored in Azure as Api__Key.",
                    Name = "X-Console-Ops-Key",
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("ConsoleOpskey", document)] = []
                });

            });
            return service;
        }
    }
}
