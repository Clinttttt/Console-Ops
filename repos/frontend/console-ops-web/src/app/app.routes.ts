import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'overview',
    title: 'Overview - Console Ops',
    data: { title: 'Overview', subtitle: 'Your .NET projects at a glance' },
    loadComponent: () => import('./features/overview/overview-page').then((m) => m.OverviewPage),
  },
  {
    path: 'projects',
    title: 'Projects - Console Ops',
    data: {
      title: 'Projects',
      subtitle: 'Register, organize, and monitor your application surfaces.',
    },
    loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
  },
  {
    path: 'projects/new',
    title: 'Add Project - Console Ops',
    data: {
      title: 'Add Project',
      subtitle: 'Register a new application surface and connect its operational context.',
    },
    loadComponent: () =>
      import('./features/projects/add-project-page').then((m) => m.AddProjectPage),
  },
  {
    path: 'deployments',
    title: 'Deployments - Console Ops',
    data: {
      title: 'Deployments',
      subtitle: 'Release history and deployment verification across your environments.',
    },
    loadComponent: () =>
      import('./features/deployments/deployments-page').then((m) => m.DeploymentsPage),
  },
  {
    path: 'environments',
    title: 'Environments - Console Ops',
    data: {
      title: 'Environments',
      subtitle: 'Runtime targets and configuration across your registered projects.',
    },
    loadComponent: () =>
      import('./features/environments/environments-page').then((m) => m.EnvironmentsPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: '**', redirectTo: 'overview' },
];
