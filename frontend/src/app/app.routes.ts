import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { ShellComponent } from './layout/shell/shell.component';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'people',
        loadComponent: () => import('./features/people/people-list.component').then(m => m.PeopleListComponent)
      },
      {
        path: 'candidates',
        loadComponent: () => import('./features/candidates/candidates-list.component').then(m => m.CandidatesListComponent)
      },
      {
        path: 'admin/users',
        loadComponent: () => import('./features/admin-users/users-list.component').then(m => m.UsersListComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
