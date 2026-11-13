export enum DiagramType {
  BusinessContext = 1,
  UseCase = 2,
  Sequence = 3,
  Activity = 4,
  State = 5,
}

export const DiagramTypeLabels: Record<DiagramType, string> = {
  [DiagramType.BusinessContext]: 'Business Context',
  [DiagramType.UseCase]: 'Use Case',
  [DiagramType.Sequence]: 'Sequence',
  [DiagramType.Activity]: 'Activity',
  [DiagramType.State]: 'State',
};

export type DiagramStatus = 'Draft' | 'Pending' | 'Rendering' | 'Rendered' | 'Failed';

export type ReviewStatus = 'Submitted' | 'Approved' | 'ChangesRequested';

export interface DiagramSummary {
  id: string;
  projectId: string;
  type: DiagramType;
  title: string;
  status: DiagramStatus;
  currentVersion: number;
  createdAt: string;
  updatedAt: string;
  renderedAt?: string | null;
  lastError?: string | null;
}

export interface DiagramDetail extends DiagramSummary {
  modelJson: string;
  plantUml?: string | null;
  svgXml?: string | null;
  eTag?: string | null;
  needsResubmit?: boolean;
  warnings?: string[];
}

export interface DiagramVersionSummary {
  id: string;
  diagramId: string;
  version: number;
  title: string;
  createdBy: string;
  createdAt: string;
}

export interface DiagramVersionDetail extends DiagramVersionSummary {
  modelJson: string;
}

export interface RenderResponse {
  diagramId: string;
  version: number;
  status: DiagramStatus;
  message: string;
}

export interface Review {
  id: string;
  projectId: string;
  status: ReviewStatus;
  submittedBy: string;
  submittedAt: string;
  decidedBy?: string | null;
  decidedAt?: string | null;
  note?: string | null;
  snapshotJson: string;
}

export interface Comment {
  id: string;
  diagramId: string;
  authorId: string;
  content: string;
  createdAt: string;
}

export interface RelationSuggestion {
  from: string;
  to: string;
  description: string;
}

export interface DiagramValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}
