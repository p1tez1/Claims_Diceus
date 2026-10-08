import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { User, UserRole } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  list(role?: UserRole): Observable<User[]> { return this.http.get<User[]>(`${environment.apiBaseUrl}/api/reference/users`, { params: role ? new HttpParams().set('role', role) : undefined }); }
}
