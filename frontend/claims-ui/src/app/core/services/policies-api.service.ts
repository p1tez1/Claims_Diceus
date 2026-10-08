import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Policy } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PoliciesApiService {
  private readonly http = inject(HttpClient);
  search(query: string): Observable<Policy[]> { return this.http.get<Policy[]>(`${environment.apiBaseUrl}/api/policies/search`, { params: new HttpParams().set('q', query) }); }
  coverage(policyId: string): Observable<string[]> { return this.http.get<string[]>(`${environment.apiBaseUrl}/api/policies/${policyId}/coverage`); }
}
