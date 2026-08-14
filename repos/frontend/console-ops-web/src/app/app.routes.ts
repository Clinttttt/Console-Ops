import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'overview',
    title: 'Overview - Console Ops',
    data: { title: 'Overview', subtitle: 'Your .NET projects at a glance' },
    loadComponent: () => import('./features/overview/overview-page').then((m) => m.OverviewPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: '**', redirectTo: 'overview' },
];
