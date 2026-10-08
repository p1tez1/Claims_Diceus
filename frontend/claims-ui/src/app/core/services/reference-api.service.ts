import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CauseOfLoss, ClaimStatusReference } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ReferenceApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/reference`;
  causes(perilCategory?: string): Observable<CauseOfLoss[]> { return this.http.get<CauseOfLoss[]>(`${this.baseUrl}/cause-of-loss-codes`, { params: perilCategory ? new HttpParams().set('perilCategory', perilCategory) : undefined }); }
  claimStatuses(): Observable<ClaimStatusReference[]> { return this.http.get<ClaimStatusReference[]>(`${this.baseUrl}/claim-statuses`); }
}
