import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditLog, Page } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly http = inject(HttpClient);
  get(claimId: string, page = 1, pageSize = 20): Observable<Page<AuditLog>> {
    return this.http.get<Page<AuditLog>>(`${environment.apiBaseUrl}/api/claims/${claimId}/audit`, { params: new HttpParams().set('page', page).set('pageSize', pageSize) });
  }
}
