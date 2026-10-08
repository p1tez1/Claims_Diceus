import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ReserveComponent, ReserveTransactionResult, SubmitReserveRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ReservesApiService {
  private readonly http = inject(HttpClient);
  private readonly claimsUrl = `${environment.apiBaseUrl}/api/claims`;
  get(claimId: string): Observable<ReserveComponent[]> { return this.http.get<ReserveComponent[]>(`${this.claimsUrl}/${claimId}/reserves`); }
  submit(claimId: string, request: SubmitReserveRequest, idempotencyKey?: string): Observable<ReserveTransactionResult> { return this.http.post<ReserveTransactionResult>(`${this.claimsUrl}/${claimId}/reserves`, request, idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : {}); }
  adjust(claimId: string, componentId: string, amount: number, changeReason: string, resubmissionOfId?: string): Observable<ReserveTransactionResult> { return this.http.put<ReserveTransactionResult>(`${this.claimsUrl}/${claimId}/reserves/${componentId}`, { amount, changeReason, resubmissionOfId }); }
  approve(claimId: string, transactionId: string): Observable<void> { return this.action(claimId, transactionId, 'approve'); }
  reject(claimId: string, transactionId: string, rejectionReason: string): Observable<void> { return this.http.post<void>(`${this.claimsUrl}/${claimId}/reserves/${transactionId}/reject`, { rejectionReason }); }
  retract(claimId: string, transactionId: string): Observable<void> { return this.action(claimId, transactionId, 'retract'); }
  retryPosting(claimId: string, transactionId: string): Observable<void> { return this.action(claimId, transactionId, 'retry-posting'); }
  setOverride(claimId: string, enabled = true): Observable<void> { return this.http.post<void>(`${this.claimsUrl}/${claimId}/reserve-override`, { enabled }); }
  private action(claimId: string, transactionId: string, action: string): Observable<void> { return this.http.post<void>(`${this.claimsUrl}/${claimId}/reserves/${transactionId}/${action}`, {}); }
}
