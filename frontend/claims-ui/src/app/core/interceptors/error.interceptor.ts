import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ErrorNotificationService } from '../services/error-notification.service';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(ErrorNotificationService);
  return next(request).pipe(catchError((error: HttpErrorResponse) => {
    notifications.notify(error);
    return throwError(() => error);
  }));
};
