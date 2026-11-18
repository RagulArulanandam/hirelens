export const API = 'http://localhost:5110/api';

export interface AuthUser { token: string; expires: string; email: string; fullName: string; roles: string[]; }

export interface Gap { area: string; detail: string; suggestion: string; }
export interface AtsCheck { name: string; passed: boolean; points: number; maxPoints: number; note: string; }
export interface AtsReport {
  score: number; keywordCoveragePct: number; keywordPoints: number;
  jdKeywords: string[]; matchedKeywords: string[]; missingKeywords: string[]; checks: AtsCheck[];
}
export interface AnalysisOutput {
  atsScore: number; cvScore: number; fitScore: number; fitVerdict: string;
  matchedSkills: string[]; missingSkills: string[]; gaps: Gap[]; strengths: string[]; redFlags: string[];
  summary: string; atsBreakdown?: AtsReport;
}
export interface CvResult {
  id: string; fileName: string; status: 'Pending' | 'Completed' | 'Failed'; error?: string;
  atsScore: number; cvScore: number; fitScore: number; verdict: string; fromCache: boolean; model: string;
  inputTokens: number; outputTokens: number; output?: AnalysisOutput;
}
export interface AnalysisSummary { id: string; roleTitle: string; createdAt: string; cvCount: number; bestFitScore?: number; bestVerdict?: string; }
export interface AnalysisDetail { id: string; roleTitle: string; jdText: string; createdAt: string; results: CvResult[]; }
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }

export interface UserRow { id: string; email: string; fullName: string; roles: string[]; createdAt: string; }
export interface UsageStats { analyses: number; cvsAnalysed: number; inputTokens: number; outputTokens: number; cacheHits: number; cacheHitRate: number; defaultModel: string; }
