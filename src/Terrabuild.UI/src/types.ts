export type ProjectInfo = {
  id: string;
  name?: string | null;
  directory: string;
  hash: string;
};

export type GraphNode = {
  id: string;
  projectId: string;
  projectName?: string | null;
  projectDir: string;
  target: string;
  phase?: string | null;
  dependencies: string[];
  projectHash: string;
  targetHash: string;
};

export type EvaluationInput = {
  name: string;
  valueHash: string;
};

export type CacheEvidence = {
  scope: string;
  key: string;
  lookup: string;
  origin?: string | null;
};

export type ResolvedOperation = {
  metaCommand: string;
  command: string;
  argumentsHash: string;
  container?: string | null;
  platform?: string | null;
  cpus?: number | null;
  forwardedVariableNames: string[];
  injectedEnvironment: Array<{
    name: string;
    valueHash: string;
  }>;
};

export type NodeExplanation = {
  id: string;
  action?: string | null;
  actionReason?: string | null;
  actionDependencies: string[];
  required?: boolean | null;
  requirementReason?: string | null;
  dependencies: string[];
  cache?: CacheEvidence | null;
  evaluationInputs: EvaluationInput[];
  resolvedOperations: ResolvedOperation[];
  fingerprint?: {
    cacheKey: string;
  } | null;
};

export type GraphResponse = {
  nodes: Record<string, GraphNode>;
  explanations: Record<string, NodeExplanation>;
  phases?: Record<string, string[]>;
  rootNodes?: string[];
  engine?: string | null;
  configuration?: string | null;
  environment?: string | null;
};

export type ProjectStatus = {
  projectId: string;
  status: "success" | "failed";
};

export type ProjectNode = {
  id: string;
  flowId: string;
  name?: string | null;
  directory: string;
  hash: string;
  phase?: string | null;
  targets: GraphNode[];
};

export type OperationSummary = {
  metaCommand: string;
  command: string;
  arguments: string;
  log: string;
  exitCode: number;
};

export type TargetSummary = {
  project: string;
  target: string;
  operations: OperationSummary[][];
  isSuccessful: boolean;
  startedAt: string;
  endedAt: string;
  duration: string;
  cache: string;
  outputs?: string | null;
};
