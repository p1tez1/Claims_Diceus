import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  { path: 'claims', canActivate: [authGuard], loadComponent: () => import('./features/claims-list/claims-list.component').then((module) => module.ClaimsListComponent), title: 'Claims' },
  { path: 'claims/new', canActivate: [authGuard], loadComponent: () => import('./features/fnol-intake/fnol-intake.component').then((module) => module.FnolIntakeComponent), title: 'New claim' },
  { path: 'claims/:id', canActivate: [authGuard], loadComponent: () => import('./features/claim-detail/claim-detail.component').then((module) => module.ClaimDetailComponent), title: 'Claim details' },
  { path: '**', redirectTo: 'claims' }
];
