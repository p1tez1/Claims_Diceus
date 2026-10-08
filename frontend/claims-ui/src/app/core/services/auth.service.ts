import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginResponse, ReserveHistory, User, UserRole } from '../models/api.models';

export interface DevUser { username: 'handler' | 'supervisor' | 'manager'; name: string; role: UserRole; }

export const DEV_USERS: readonly DevUser[] = [
  { username: 'handler', name: 'Claims Handler', role: 'Handler' },
  { username: 'supervisor', name: 'Claims Supervisor', role: 'Supervisor' },
  { username: 'manager', name: 'Claims Manager', role: 'Manager' }
];

interface StoredSession { token: string; user: User; username: DevUser['username']; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly sessionKey = 'claims-ui.session';
  private readonly tokenState = signal<string | null>(null);
  private readonly userState = signal<User | null>(null);
  private readonly usernameState = signal<DevUser['username']>('handler');
  private initialization?: Observable<LoginResponse | null>;

  readonly currentUser = this.userState.asReadonly();
  readonly username = this.usernameState.asReadonly();
  readonly isAuthenticated = computed(() => this.tokenState() !== null);
  readonly isSupervisorOrAbove = computed(() => this.hasRole('Supervisor'));
  readonly isManager = computed(() => this.hasRole('Manager'));

  constructor() { this.restoreSession(); }

  get token(): string | null { return this.tokenState(); }

  initialize(): Observable<LoginResponse | null> {
    if (this.initialization) return this.initialization;
    if (this.tokenState() && !this.tokenExpired(this.tokenState()!)) return of(null);
    this.logout();
    this.initialization = this.login(this.usernameState()).pipe(
      catchError(() => { this.logout(); return of(null); }),
      finalize(() => this.initialization = undefined),
      shareReplay({ bufferSize: 1, refCount: false })
    );
    return this.initialization;
  }

  recover(rejectedToken: string | null): Observable<LoginResponse | null> {
    if (this.tokenState() && this.tokenState() !== rejectedToken) return of(null);
    this.logout();
    return this.initialize();
  }

  login(username: DevUser['username']): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/dev-login`, { username }).pipe(
      tap((response) => {
        this.tokenState.set(response.accessToken);
        this.userState.set({ ...response.user, username });
        this.usernameState.set(username);
        sessionStorage.setItem(this.sessionKey, JSON.stringify({ token: response.accessToken, user: response.user, username } satisfies StoredSession));
      })
    );
  }

  switchRole(role: UserRole): Observable<LoginResponse> {
    const user = DEV_USERS.find((candidate) => candidate.role === role) ?? DEV_USERS[0];
    return this.login(user.username);
  }

  logout(): void {
    this.tokenState.set(null); this.userState.set(null); sessionStorage.removeItem(this.sessionKey);
  }

  hasRole(minimumRole: UserRole): boolean {
    const order: Record<UserRole, number> = { Handler: 0, Supervisor: 1, Manager: 2 };
    const role = this.userState()?.role;
    return role !== undefined && order[role] >= order[minimumRole];
  }

  canReviewReserve(row: ReserveHistory): boolean {
    const user = this.currentUser();
    return row.approvalStatus === 'PendingApproval' && !!user && this.isSupervisorOrAbove()
      && user.id !== row.submittedByUserId && Math.abs(row.amount) <= (this.isManager() ? 10_000_000 : 100_000);
  }

  reserveReviewBlockReason(row: ReserveHistory): string {
    if (this.currentUser()?.id === row.submittedByUserId) return 'You cannot review your own reserve.';
    if (Math.abs(row.amount) > (this.isManager() ? 10_000_000 : 100_000)) return 'Amount exceeds your approval authority.';
    return 'Review unavailable.';
  }

  canRetractReserve(row: ReserveHistory): boolean {
    return row.approvalStatus === 'PendingApproval' && !!this.currentUser() && this.currentUser()?.id === row.submittedByUserId;
  }

  canRetryPosting(row: ReserveHistory): boolean { return this.hasRole('Handler') && row.postingStatus === 'Failed'; }

  private restoreSession(): void {
    const value = sessionStorage.getItem(this.sessionKey);
    if (!value) return;
    try {
      const session = JSON.parse(value) as StoredSession;
      if (!DEV_USERS.some((user) => user.username === session.username)) throw new Error('Unknown development user.');
      this.usernameState.set(session.username);
      if (this.tokenExpired(session.token)) throw new Error('Expired session.');
      this.tokenState.set(session.token); this.userState.set(session.user); this.usernameState.set(session.username);
    } catch {
      sessionStorage.removeItem(this.sessionKey);
    }
  }

  private tokenExpired(token: string): boolean {
    try {
      const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const expiry: unknown = JSON.parse(atob(payload)).exp;
      return typeof expiry !== 'number' || expiry * 1000 <= Date.now();
    } catch { return true; }
  }
}
