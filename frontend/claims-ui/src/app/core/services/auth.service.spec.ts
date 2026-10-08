import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('logs in through dev-login and holds the user and token', () => {
    service.login('supervisor').subscribe();
    const request = http.expectOne('/api/auth/dev-login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ username: 'supervisor' });
    request.flush({ accessToken: 'jwt', user: { id: 'user-2', name: 'Claims Supervisor', role: 'Supervisor' } });
    expect(service.token).toBe('jwt');
    expect(service.currentUser()?.role).toBe('Supervisor');
    expect(service.isSupervisorOrAbove()).toBeTrue();
    expect(sessionStorage.getItem('claims-ui.session')).toContain('jwt');
  });

  it('switches to the backend seeded manager username', () => {
    service.switchRole('Manager').subscribe();
    const request = http.expectOne('/api/auth/dev-login');
    expect(request.request.body).toEqual({ username: 'manager' });
    request.flush({ accessToken: 'manager-jwt', user: { id: 'user-3', name: 'Claims Manager', role: 'Manager' } });
    expect(service.isManager()).toBeTrue();
  });

  it('F03 shares a delayed initial login across protected navigations', () => {
    let completed = 0;
    service.initialize().subscribe(() => completed++);
    service.initialize().subscribe(() => completed++);
    const requests = http.match('/api/auth/dev-login');
    expect(requests.length).toBe(1);
    expect(completed).toBe(0);
    requests.forEach(request => request.flush({ accessToken: 'jwt', user: { id: 'user-1', role: 'Handler', name: 'Handler' } }));
    expect(completed).toBe(2);
  });

  it('F03 replaces an expired restored token using the previous acting role', () => {
    const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) - 60 }));
    sessionStorage.setItem('claims-ui.session', JSON.stringify({ token: `header.${payload}.signature`, user: { id: 'user-3', role: 'Manager' }, username: 'manager' }));
    const restored = TestBed.runInInjectionContext(() => new AuthService());
    expect(restored.token).toBeNull();
    restored.initialize().subscribe();
    const login = http.expectOne('/api/auth/dev-login');
    expect(login.request.body).toEqual({ username: 'manager' });
    login.flush({ accessToken: 'jwt', user: { id: 'user-3', role: 'Manager', name: 'Manager' } });
  });
});
