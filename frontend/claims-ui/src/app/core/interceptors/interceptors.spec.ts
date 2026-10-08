import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { errorInterceptor } from './error.interceptor';
import { loadingInterceptor } from './loading.interceptor';
import { AuthService } from '../services/auth.service';
import { LoadingService } from '../services/loading.service';
import { ErrorNotificationService } from '../services/error-notification.service';

describe('HTTP interceptors', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let loading: LoadingService;
  let notifications: jasmine.SpyObj<ErrorNotificationService>;
  beforeEach(() => {
    notifications = jasmine.createSpyObj<ErrorNotificationService>('ErrorNotificationService', ['notify']);
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authInterceptor, loadingInterceptor, errorInterceptor])), provideHttpClientTesting(),
      { provide: AuthService, useValue: { token: 'test-token' } },
      { provide: ErrorNotificationService, useValue: notifications }
    ] });
    client = TestBed.inject(HttpClient); http = TestBed.inject(HttpTestingController); loading = TestBed.inject(LoadingService);
  });
  afterEach(() => http.verify());

  it('adds a bearer token and tracks in-flight requests', () => {
    client.get('/api/test').subscribe();
    expect(loading.pendingRequests()).toBe(1);
    const request = http.expectOne('/api/test'); expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
    request.flush({}); expect(loading.pendingRequests()).toBe(0);
  });

  it('notifies while preserving the original backend error for forms', () => {
    let received: HttpErrorResponse | undefined;
    client.get('/api/failure').subscribe({ error: (error: HttpErrorResponse) => received = error });
    const request = http.expectOne('/api/failure');
    const body = { type: 'ValidationError', title: 'Invalid', status: 422, errors: { LossDate: ['Future dates are invalid.'] } };
    request.flush(body, { status: 422, statusText: 'Unprocessable Entity' });
    expect(notifications.notify).toHaveBeenCalled();
    expect(received?.error).toEqual(body);
    expect(loading.pendingRequests()).toBe(0);
  });
});

describe('F03 expired-session recovery', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let auth: AuthService;
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController); client = TestBed.inject(HttpClient); auth = TestBed.inject(AuthService);
    auth.login('manager').subscribe();
    http.expectOne('/api/auth/dev-login').flush({ accessToken: 'old-token', user: { id: 'manager', name: 'Manager', role: 'Manager' } });
  });
  afterEach(() => http.verify());

  it('shares one recovery login and retries requests with the same body and idempotency key', () => {
    client.post('/api/claims/claim/reserves', { amount: 100 }, { headers: { 'Idempotency-Key': 'original-key' } }).subscribe();
    client.get('/api/claims').subscribe();
    const reserve = http.expectOne('/api/claims/claim/reserves'), claims = http.expectOne('/api/claims');
    reserve.flush({}, { status: 401, statusText: 'Unauthorized' });
    claims.flush({}, { status: 401, statusText: 'Unauthorized' });
    const login = http.expectOne('/api/auth/dev-login');
    expect(login.request.body).toEqual({ username: 'manager' });
    login.flush({ accessToken: 'new-token', user: { id: 'manager', name: 'Manager', role: 'Manager' } });
    const retry = http.expectOne('/api/claims/claim/reserves');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    expect(retry.request.headers.get('Idempotency-Key')).toBe('original-key');
    expect(retry.request.body).toEqual({ amount: 100 }); retry.flush({});
    http.expectOne('/api/claims').flush([]);
  });

  it('does not recurse when the login endpoint returns 401', () => {
    let received = 0;
    client.get('/api/claims').subscribe({ error: error => received = error.status });
    http.expectOne('/api/claims').flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/auth/dev-login').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(received).toBe(401); expect(auth.token).toBeNull();
  });
});
