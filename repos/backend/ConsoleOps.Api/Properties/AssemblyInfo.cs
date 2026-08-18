using System.Runtime.CompilerServices;

// The host's own clamping rules - a retention window's floor, a batch size's ceiling - are worth asserting
// without widening the public surface to do it. Same arrangement as Application and Infrastructure.
[assembly: InternalsVisibleTo("ConsoleOps.Tests.Integration")]
