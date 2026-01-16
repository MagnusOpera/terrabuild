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
  dependencies: string[];
  projectHash: string;
  targetHash: string;
};

export type GraphResponse = {
  nodes: Record<string, GraphNode>;
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
  name?: string | null;
  directory: string;
  hash: string;
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
