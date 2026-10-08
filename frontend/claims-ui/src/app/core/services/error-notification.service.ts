import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError } from '../models/api.models';

export type MessageSeverity = 'info' | 'warning' | 'error';

@Injectable({ providedIn: 'root' })
export class ErrorNotificationService {
  private readonly snackBar = inject(MatSnackBar);
  private readonly apiErrorState = signal<ApiError | null>(null);
  readonly lastApiError = this.apiErrorState.asReadonly();

  notify(error: HttpErrorResponse): void {
    const body = this.asApiError(error);
    this.apiErrorState.set(body);
    const detail = Object.values(body.errors).flat()[0];
    const severity = this.severity(body.status);
    this.snackBar.open(detail ?? body.title ?? 'The request could not be completed.', 'Dismiss', {
      duration: severity === 'error' ? 7000 : 5000,
      panelClass: [`snackbar-${severity}`]
    });
  }

  fieldErrors(field: string): readonly string[] { return this.apiErrorState()?.errors[field] ?? []; }
  clear(): void { this.apiErrorState.set(null); }

  private asApiError(error: HttpErrorResponse): ApiError {
    const body = error.error;
    if (body && typeof body === 'object' && typeof body.title === 'string') {
      return { type: body.type ?? 'RequestError', title: body.title, status: body.status ?? error.status, errors: body.errors ?? {}, correlationId: body.correlationId };
    }
    return { type: 'NetworkError', title: error.status === 0 ? 'Unable to reach the claims service.' : error.message, status: error.status, errors: {} };
  }

  private severity(status: number): MessageSeverity { return status >= 500 || status === 0 ? 'error' : status === 409 || status === 422 ? 'warning' : 'info'; }
}
