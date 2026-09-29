import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';

interface RequestItem {
  id: string; requesterReference: string; description: string; amount: number; currency: string;
  status: string; outcome: string | null; createdAt: string;
}
interface ApprovalTask { id: string; workflowInstanceId: string; request: RequestItem; createdAt: string; }
interface RequestDetails { request: RequestItem; events: { type: string; actorSubject: string; dataJson: string; occurredAt: string }[]; }

@Component({
  selector: 'app-root',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  readonly currentSubject = localStorage.getItem('demo-subject') ?? 'requester@example.test';
  actor = this.currentSubject;
  accessToken = sessionStorage.getItem('api-access-token') ?? '';
  requests: RequestItem[] = [];
  tasks: ApprovalTask[] = [];
  selected: RequestDetails | null = null;
  busy = false;
  error = '';
  success = '';
  form = { requesterReference: 'REQ-1001', description: '', amount: 1500, currency: 'USD' };

  ngOnInit(): void { this.refresh(); }

  refresh(): void {
    this.error = '';
    this.http.get<RequestItem[]>('/api/v1/purchase-requests').subscribe({ next: value => this.requests = value, error: error => this.showError(error) });
    this.http.get<ApprovalTask[]>('/api/v1/approval-tasks').subscribe({ next: value => this.tasks = value, error: error => this.showError(error) });
  }

  switchActor(): void {
    localStorage.setItem('demo-subject', this.actor.trim());
    location.reload();
  }

  saveAccessToken(): void {
    if (this.accessToken.trim()) sessionStorage.setItem('api-access-token', this.accessToken.trim());
    else sessionStorage.removeItem('api-access-token');
  }

  createRequest(): void {
    this.busy = true; this.error = ''; this.success = '';
    this.http.post<RequestItem>('/api/v1/purchase-requests', this.form, { headers: { 'Idempotency-Key': crypto.randomUUID() } }).subscribe({
      next: value => { this.busy = false; this.success = `Request ${value.requesterReference} started.`; this.form = { ...this.form, requesterReference: '', description: '' }; this.refresh(); this.open(value.id); },
      error: error => { this.busy = false; this.showError(error); }
    });
  }

  open(id: string): void {
    this.selected = null;
    this.http.get<RequestDetails>(`/api/v1/purchase-requests/${id}`).subscribe({ next: value => this.selected = value, error: error => this.showError(error) });
  }

  decide(task: ApprovalTask, decision: 'approve' | 'reject'): void {
    const comment = decision === 'reject' ? 'Rejected from the operations console.' : 'Approved from the operations console.';
    this.http.post(`/api/v1/approval-tasks/${task.id}/decision`, { decision, comment }).subscribe({
      next: () => { this.success = `Request ${task.request.requesterReference} ${decision === 'approve' ? 'approved' : 'rejected'}.`; this.refresh(); this.open(task.workflowInstanceId); },
      error: error => this.showError(error)
    });
  }

  private showError(error: HttpErrorResponse): void {
    this.error = error.status === 0 ? 'Could not reach the API. Start PostgreSQL and the API, then refresh.' : error.error?.error ?? error.error?.title ?? `Request failed (${error.status}).`;
  }
}
