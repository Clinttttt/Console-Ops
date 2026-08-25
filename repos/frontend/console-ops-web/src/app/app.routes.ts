import { Routes } from '@angular/router';

import { operatorGuard } from './core/state/operator.guard';

export const routes: Routes = [
  {
    // The only screen that answers before a session exists, so it is deliberately outside the guard.
    path: 'sign-in',
    title: 'Sign in - Console Ops',
    loadComponent: () => import('./features/authentication/sign-in-page').then((m) => m.SignInPage),
  },
  {
    path: 'overview',
    canActivate: [operatorGuard],
    title: 'Overview - Console Ops',
    data: { title: 'Overview', subtitle: 'Your .NET projects at a glance' },
    loadComponent: () => import('./features/overview/overview-page').then((m) => m.OverviewPage),
  },
  {
    path: 'projects',
    canActivate: [operatorGuard],
    title: 'Projects - Console Ops',
    data: {
      title: 'Projects',
      subtitle: 'Register, organize, and monitor your application surfaces.',
    },
    loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
  },
  {
    path: 'projects/new',
    canActivate: [operatorGuard],
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
    canActivate: [operatorGuard],
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
    canActivate: [operatorGuard],
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
    canActivate: [operatorGuard],
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
    canActivate: [operatorGuard],
    title: 'Workflows - Console Ops',
    data: {
      title: 'Workflows',
      subtitle: 'Automation inventory and recent run activity across your connected repositories.',
    },
    loadComponent: () => import('./features/workflows/workflows-page').then((m) => m.WorkflowsPage),
  },
  {
    // Route parameters are bound to the page's inputs, so the screen can be opened directly.
    path: 'workflows/:projectId/:workflowId/runs',
    canActivate: [operatorGuard],
    title: 'Workflow runs - Console Ops',
    data: {
      title: 'Workflow runs',
      subtitle: 'Recent executions of one workflow, and the jobs of the run you open.',
    },
    loadComponent: () =>
      import('./features/workflows/workflow-runs-page').then((m) => m.WorkflowRunsPage),
  },
  {
    path: 'environments',
    canActivate: [operatorGuard],
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
    canActivate: [operatorGuard],
    title: 'Health - Console Ops',
    data: {
      title: 'Health',
      subtitle: 'Current application and dependency state across monitored environments.',
    },
    loadComponent: () => import('./features/health/health-page').then((m) => m.HealthPage),
  },
  {
    path: 'logs',
    canActivate: [operatorGuard],
    title: 'Logs - Console Ops',
    data: {
      title: 'Logs',
      subtitle: 'Application and runtime events across your environments.',
    },
    loadComponent: () => import('./features/logs/logs-page').then((m) => m.LogsPage),
  },
  {
    path: 'settings',
    canActivate: [operatorGuard],
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
