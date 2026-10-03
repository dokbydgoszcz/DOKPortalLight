import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { Permissions } from './core/auth/permissions';
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
        path: 'name-days',
        loadComponent: () => import('./features/name-days/name-days-list.component').then(m => m.NameDaysListComponent)
      },
      {
        path: 'documents',
        canActivate: [permissionGuard(Permissions.DocumentsView)],
        loadComponent: () => import('./features/documents/documents.component').then(m => m.DocumentsComponent)
      },
      {
        path: 'mailing',
        canActivate: [permissionGuard(Permissions.MailingView)],
        loadComponent: () => import('./features/mailing/mailing.component').then(m => m.MailingComponent)
      },
      {
        path: 'candidates',
        canActivate: [permissionGuard(Permissions.CandidatesView)],
        loadComponent: () => import('./features/candidates/candidates-list.component').then(m => m.CandidatesListComponent)
      },
      {
        path: 'missions',
        canActivate: [permissionGuard(Permissions.MissionsView)],
        loadComponent: () => import('./features/missions/missions-list.component').then(m => m.MissionsListComponent)
      },
      {
        path: 'formators',
        canActivate: [permissionGuard(Permissions.FormatorsView)],
        loadComponent: () => import('./features/formators/formators-list.component').then(m => m.FormatorsListComponent)
      },
      {
        path: 'parish-board',
        canActivate: [permissionGuard(Permissions.ParishNeedsView)],
        loadComponent: () => import('./features/parish-board/parish-board.component').then(m => m.ParishBoardComponent)
      },
      {
        path: 'parishes',
        canActivate: [permissionGuard(Permissions.ParishesManage)],
        loadComponent: () => import('./features/parish-board/parishes-list.component').then(m => m.ParishesListComponent)
      },
      {
        path: 'budget/sksp',
        canActivate: [permissionGuard(Permissions.BudgetSkspView)],
        loadComponent: () => import('./features/budget/budget.component').then(m => m.BudgetComponent)
      },
      {
        path: 'dok-cases',
        canActivate: [permissionGuard(Permissions.DokCasesView)],
        loadComponent: () => import('./features/dok-cases/dok-cases-list.component').then(m => m.DokCasesListComponent)
      },
      {
        path: 'meetings',
        canActivate: [permissionGuard(Permissions.MeetingsView)],
        loadComponent: () => import('./features/meetings/meetings-list.component').then(m => m.MeetingsListComponent)
      },
      {
        path: 'supervisions',
        canActivate: [permissionGuard(Permissions.SupervisionsView)],
        loadComponent: () => import('./features/supervisions/supervisions-list.component').then(m => m.SupervisionsListComponent)
      },
      {
        path: 'graduates',
        canActivate: [permissionGuard(Permissions.GraduatesView)],
        loadComponent: () => import('./features/graduates/graduates-list.component').then(m => m.GraduatesListComponent)
      },
      {
        path: 'budget/dok',
        canActivate: [permissionGuard(Permissions.BudgetDokView)],
        loadComponent: () => import('./features/budget-dok/budget-dok.component').then(m => m.BudgetDokComponent)
      },
      {
        path: 'admin/users',
        canActivate: [permissionGuard(Permissions.UsersManage)],
        loadComponent: () => import('./features/admin-users/users-list.component').then(m => m.UsersListComponent)
      },
      {
        path: 'admin/permissions',
        canActivate: [permissionGuard(Permissions.PermissionsManage)],
        loadComponent: () => import('./features/admin-permissions/permissions-matrix.component').then(m => m.PermissionsMatrixComponent)
      },
      {
        path: 'audit-log',
        canActivate: [permissionGuard(Permissions.AuditLogView)],
        loadComponent: () => import('./features/audit-log/audit-log.component').then(m => m.AuditLogComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
