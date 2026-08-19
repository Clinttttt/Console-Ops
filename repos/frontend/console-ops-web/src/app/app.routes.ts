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
    path: 'projects/:projectId/edit',
    title: 'Edit Project - Console Ops',
    data: {
      title: 'Edit Project',
      subtitle: 'Change what Console Ops observes for this project.',
    },
    loadComponent: () =>
      import('./features/projects/edit-project-page').then((m) => m.EditProjectPage),
  },
  {
    path: 'projects/:projectId',
    title: 'Project - Console Ops',
    data: {
      title: 'Project',
      subtitle: 'Configuration and the latest observations for one project.',
    },
    loadComponent: () =>
      import('./features/projects/project-detail-page').then((m) => m.ProjectDetailPage),
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
    path: 'workflows',
    title: 'Workflows - Console Ops',
    data: {
      title: 'Workflows',
      subtitle: 'Automation inventory and recent run activity across your connected repositories.',
    },
    loadComponent: () => import('./features/workflows/workflows-page').then((m) => m.WorkflowsPage),
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
  {
    path: 'health',
    title: 'Health - Console Ops',
    data: {
      title: 'Health',
      subtitle: 'Current application and dependency state across monitored environments.',
    },
    loadComponent: () => import('./features/health/health-page').then((m) => m.HealthPage),
  },
  {
    path: 'logs',
    title: 'Logs - Console Ops',
    data: {
      title: 'Logs',
      subtitle: 'Application and runtime events across your environments.',
    },
    loadComponent: () => import('./features/logs/logs-page').then((m) => m.LogsPage),
  },
  {
    path: 'settings',
    title: 'Settings - Console Ops',
    data: {
      title: 'Settings',
      subtitle: 'Configure Console Ops connections and observation behavior.',
    },
    loadComponent: () => import('./features/settings/settings-page').then((m) => m.SettingsPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: '**', redirectTo: 'overview' },
];
