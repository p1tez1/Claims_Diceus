import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly requests = signal(0);
  readonly pendingRequests = this.requests.asReadonly();
  readonly isLoading = computed(() => this.requests() > 0);
  start(): void { this.requests.update((count) => count + 1); }
  stop(): void { this.requests.update((count) => Math.max(0, count - 1)); }
}
