import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  const authorized = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
  return next(authorized).pipe(catchError((error: HttpErrorResponse) => {
    if (error.status !== 401 || request.url.includes('/api/auth/dev-login') || !request.url.startsWith(`${environment.apiBaseUrl}/api/`))
      return throwError(() => error);
    return auth.recover(token).pipe(switchMap(() => auth.token
      ? next(request.clone({ setHeaders: { Authorization: `Bearer ${auth.token}` } }))
      : throwError(() => error)));
  }));
};
