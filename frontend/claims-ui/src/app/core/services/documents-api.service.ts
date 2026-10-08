import { HttpClient, HttpEvent } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DocumentInfo } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class DocumentsApiService {
  private readonly http = inject(HttpClient);
  private readonly claimsUrl = `${environment.apiBaseUrl}/api/claims`;
  list(claimId: string): Observable<DocumentInfo[]> { return this.http.get<DocumentInfo[]>(`${this.claimsUrl}/${claimId}/documents`); }
  upload(claimId: string, file: File, documentType: string, notes?: string): Observable<DocumentInfo> {
    const body = new FormData();
    body.append('file', file); body.append('documentType', documentType);
    if (notes) body.append('notes', notes);
    return this.http.post<DocumentInfo>(`${this.claimsUrl}/${claimId}/documents`, body);
  }
  uploadWithProgress(claimId: string, file: File, documentType: string): Observable<HttpEvent<DocumentInfo>> {
    const body = new FormData();
    body.append('file', file); body.append('documentType', documentType);
    return this.http.post<DocumentInfo>(`${this.claimsUrl}/${claimId}/documents`, body, { observe: 'events', reportProgress: true });
  }
}
