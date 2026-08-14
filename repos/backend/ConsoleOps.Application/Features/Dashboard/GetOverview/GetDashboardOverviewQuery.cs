using MediatR;

namespace ConsoleOps.Application.Features.Dashboard.GetOverview;

public sealed record GetDashboardOverviewQuery : IRequest<DashboardOverviewResponse>;
