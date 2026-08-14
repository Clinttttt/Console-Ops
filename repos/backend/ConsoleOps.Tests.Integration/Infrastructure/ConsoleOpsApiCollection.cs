namespace ConsoleOps.Tests.Integration.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ConsoleOpsApiCollection : ICollectionFixture<ConsoleOpsApiFactory>
{
    public const string Name = "Console Ops API";
}
