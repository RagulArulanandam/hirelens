import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API, AnalysisDetail, AnalysisSummary, CvResult, Paged, UsageStats, UserRow } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  createAnalysis(roleTitle: string, jdText: string, jdFile: File | null) {
    const f = new FormData();
    f.append('roleTitle', roleTitle);
    if (jdFile) f.append('jdFile', jdFile); else f.append('jdText', jdText);
    return this.http.post<AnalysisDetail>(`${API}/analyses`, f);
  }

  addCv(id: string, file: File) {
    const f = new FormData();
    f.append('file', file);
    return this.http.post<CvResult>(`${API}/analyses/${id}/cvs`, f);
  }

  list(search: string, from: string, to: string, page: number, pageSize = 10) {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) p = p.set('search', search);
    if (from) p = p.set('from', from);
    if (to) p = p.set('to', to);
    return this.http.get<Paged<AnalysisSummary>>(`${API}/analyses`, { params: p });
  }

  get(id: string) { return this.http.get<AnalysisDetail>(`${API}/analyses/${id}`); }
  delete(id: string) { return this.http.delete<void>(`${API}/analyses/${id}`); }
  exportPdf(id: string) { return this.http.get(`${API}/analyses/${id}/export`, { responseType: 'blob' }); }

  // admin
  users() { return this.http.get<UserRow[]>(`${API}/admin/users`); }
  createUser(u: { email: string; fullName: string; password: string; role: string }) { return this.http.post<UserRow>(`${API}/admin/users`, u); }
  deleteUser(id: string) { return this.http.delete<void>(`${API}/admin/users/${id}`); }
  stats() { return this.http.get<UsageStats>(`${API}/admin/stats`); }
  setModel(model: string) { return this.http.put<{ defaultModel: string }>(`${API}/admin/settings/model`, { model }); }
  getSettings() { return this.http.get<{ defaultModel: string; retentionDays: number; provider: string }>(`${API}/admin/settings`); }
  setRetention(days: number) { return this.http.put<{ retentionDays: number }>(`${API}/admin/settings/retention`, { days }); }
}
