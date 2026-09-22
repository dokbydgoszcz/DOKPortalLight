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
        path: 'missions',
        loadComponent: () => import('./features/missions/missions-list.component').then(m => m.MissionsListComponent)
      },
      {
        path: 'formators',
        loadComponent: () => import('./features/formators/formators-list.component').then(m => m.FormatorsListComponent)
      },
      {
        path: 'parish-board',
        loadComponent: () => import('./features/parish-board/parish-board.component').then(m => m.ParishBoardComponent)
      },
      {
        path: 'budget/sksp',
        loadComponent: () => import('./features/budget/budget.component').then(m => m.BudgetComponent)
      },
      {
        path: 'dok-cases',
        loadComponent: () => import('./features/dok-cases/dok-cases-list.component').then(m => m.DokCasesListComponent)
      },
      {
        path: 'meetings',
        loadComponent: () => import('./features/meetings/meetings-list.component').then(m => m.MeetingsListComponent)
      },
      {
        path: 'supervisions',
        loadComponent: () => import('./features/supervisions/supervisions-list.component').then(m => m.SupervisionsListComponent)
      },
      {
        path: 'graduates',
        loadComponent: () => import('./features/graduates/graduates-list.component').then(m => m.GraduatesListComponent)
      },
      {
        path: 'budget/dok',
        loadComponent: () => import('./features/budget-dok/budget-dok.component').then(m => m.BudgetDokComponent)
      },
      {
        path: 'admin/users',
        loadComponent: () => import('./features/admin-users/users-list.component').then(m => m.UsersListComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
