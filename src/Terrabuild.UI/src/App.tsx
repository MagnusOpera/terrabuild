import { useEffect, useMemo, useRef, useState } from "react";
import ReactFlow, { Background, Controls, Node, Edge } from "reactflow";
import "reactflow/dist/style.css";
import dagre from "dagre";
import { Terminal } from "xterm";
import { FitAddon } from "xterm-addon-fit";
import "xterm/css/xterm.css";

type ProjectInfo = {
  id: string;
  name?: string | null;
  directory: string;
  hash: string;
};

type GraphNode = {
  id: string;
  projectId: string;
  projectName?: string | null;
  projectDir: string;
  target: string;
  dependencies: string[];
  projectHash: string;
  targetHash: string;
};

type GraphResponse = {
  nodes: Record<string, GraphNode>;
};

type TargetSummary = {
  project: string;
  target: string;
  isSuccessful: boolean;
  startedAt: string;
  endedAt: string;
  duration: string;
  cache: string;
  outputs?: string | null;
};

const nodeWidth = 240;
const nodeHeight = 80;

const layoutGraph = (nodes: Node[], edges: Edge[]) => {
  const graph = new dagre.graphlib.Graph();
  graph.setDefaultEdgeLabel(() => ({}));
  graph.setGraph({ rankdir: "LR", nodesep: 50, ranksep: 80 });

  nodes.forEach((node) => {
    graph.setNode(node.id, { width: nodeWidth, height: nodeHeight });
  });
  edges.forEach((edge) => graph.setEdge(edge.source, edge.target));

  dagre.layout(graph);

  const layoutedNodes = nodes.map((node) => {
    const position = graph.node(node.id);
    return {
      ...node,
      position: {
        x: position.x - nodeWidth / 2,
        y: position.y - nodeHeight / 2,
      },
    };
  });

  return { nodes: layoutedNodes, edges };
};

const App = () => {
  const [targets, setTargets] = useState<string[]>([]);
  const [projects, setProjects] = useState<ProjectInfo[]>([]);
  const [selectedTargets, setSelectedTargets] = useState<string[]>([]);
  const [selectedProjects, setSelectedProjects] = useState<string[]>([]);
  const [graph, setGraph] = useState<GraphResponse | null>(null);
  const [graphError, setGraphError] = useState<string | null>(null);
  const [buildError, setBuildError] = useState<string | null>(null);
  const [buildRunning, setBuildRunning] = useState(false);
  const [forceBuild, setForceBuild] = useState(false);
  const [retryBuild, setRetryBuild] = useState(false);
  const [parallelism, setParallelism] = useState("");
  const [selectedNode, setSelectedNode] = useState<GraphNode | null>(null);
  const [nodeResults, setNodeResults] = useState<Record<string, TargetSummary>>(
    {}
  );
  const [theme, setTheme] = useState<"dark" | "light">("dark");

  useEffect(() => {
    document.body.dataset.theme = theme;
  }, [theme]);

  const terminalRef = useRef<HTMLDivElement | null>(null);
  const terminal = useRef<Terminal | null>(null);
  const fitAddon = useRef<FitAddon | null>(null);
  const logAbort = useRef<AbortController | null>(null);

  useEffect(() => {
    const term = new Terminal({
      convertEol: false,
      scrollback: 3000,
      fontSize: 12,
      theme: {
        background: "#0e0f12",
        foreground: "#d8e1e6",
        selectionBackground: "#414b57",
      },
    });
    const fit = new FitAddon();
    term.loadAddon(fit);
    terminal.current = term;
    fitAddon.current = fit;
    if (terminalRef.current) {
      term.open(terminalRef.current);
      fit.fit();
    }
    const handleResize = () => fit.fit();
    window.addEventListener("resize", handleResize);
    return () => {
      window.removeEventListener("resize", handleResize);
      term.dispose();
    };
  }, []);

  useEffect(() => {
    const load = async () => {
      const [targetsRes, projectsRes] = await Promise.all([
        fetch("/api/targets"),
        fetch("/api/projects"),
      ]);
      if (targetsRes.ok) {
        setTargets(await targetsRes.json());
      }
      if (projectsRes.ok) {
        setProjects(await projectsRes.json());
      }
    };
    load().catch(() => null);
  }, []);

  useEffect(() => {
    const fetchGraph = async () => {
      if (selectedTargets.length === 0) {
        setGraph(null);
        setGraphError("Select at least one target to load the graph.");
        return;
      }
      setGraphError(null);
      const params = new URLSearchParams();
      selectedTargets.forEach((target) => params.append("targets", target));
      selectedProjects.forEach((project) => params.append("projects", project));
      const response = await fetch(`/api/graph?${params.toString()}`);
      if (!response.ok) {
        setGraphError(await response.text());
        setGraph(null);
        return;
      }
      const data = (await response.json()) as GraphResponse;
      setGraph(data);
    };
    fetchGraph().catch(() => {
      setGraphError("Failed to load graph.");
      setGraph(null);
    });
  }, [selectedTargets, selectedProjects]);

  const { nodes, edges } = useMemo(() => {
    if (!graph) {
      return { nodes: [], edges: [] };
    }
    const rawNodes = Object.values(graph.nodes);
    const flowNodes: Node[] = rawNodes.map((node) => ({
      id: node.id,
      data: {
        label: `${node.projectName ?? node.projectId} - ${node.target}`,
        meta: node,
      },
      position: { x: 0, y: 0 },
      className: "tb-node",
    }));
    const flowEdges: Edge[] = rawNodes.flatMap((node) =>
      node.dependencies.map((dependency) => ({
        id: `${dependency}-${node.id}`,
        source: dependency,
        target: node.id,
        type: "smoothstep",
        className: "tb-edge",
      }))
    );
    return layoutGraph(flowNodes, flowEdges);
  }, [graph]);

  const handleSelectTargets = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const values = Array.from(event.target.selectedOptions).map(
      (option) => option.value
    );
    setSelectedTargets(values);
  };

  const handleSelectProjects = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const values = Array.from(event.target.selectedOptions).map(
      (option) => option.value
    );
    setSelectedProjects(values);
  };

  const startLogStream = async () => {
    if (!terminal.current) {
      return;
    }
    terminal.current.reset();
    logAbort.current?.abort();
    const controller = new AbortController();
    logAbort.current = controller;

    const response = await fetch("/api/build/log", { signal: controller.signal });
    if (!response.body) {
      return;
    }
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }
      if (value) {
        const chunk = decoder.decode(value, { stream: true });
        terminal.current.write(chunk);
      }
    }
    setBuildRunning(false);
  };

  const startBuild = async () => {
    if (selectedTargets.length === 0 || buildRunning) {
      return;
    }
    setBuildRunning(true);
    setBuildError(null);
    const parallel =
      parallelism.trim().length > 0 ? Number(parallelism) : null;
    const payload = {
      targets: selectedTargets,
      projects: selectedProjects.length > 0 ? selectedProjects : null,
      parallelism: parallel && parallel > 0 ? parallel : null,
      force: forceBuild,
      retry: retryBuild,
    };
    const response = await fetch("/api/build", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!response.ok) {
      setBuildRunning(false);
      setBuildError(await response.text());
      return;
    }
    startLogStream().catch(() => {
      setBuildRunning(false);
    });
  };

  const loadNodeResult = async (node: GraphNode) => {
    setSelectedNode(node);
    const cacheKey = `${node.projectHash}/${node.target}/${node.targetHash}`;
    if (nodeResults[cacheKey]) {
      return;
    }
    const response = await fetch(
      `/api/build/result/${node.projectHash}/${node.target}/${node.targetHash}`
    );
    if (!response.ok) {
      return;
    }
    const summary = (await response.json()) as TargetSummary;
    setNodeResults((prev) => ({ ...prev, [cacheKey]: summary }));
  };

  const selectedResult =
    selectedNode &&
    nodeResults[
      `${selectedNode.projectHash}/${selectedNode.target}/${selectedNode.targetHash}`
    ];

  return (
    <div className="app">
      <aside className="panel">
        <div className="panel-header">
          <div>
            <p className="eyebrow">Terrabuild</p>
            <h1>Graph Console</h1>
          </div>
          <button
            className="ghost"
            onClick={() =>
              setTheme((current) => (current === "dark" ? "light" : "dark"))
            }
          >
            {theme === "dark" ? "Light mode" : "Dark mode"}
          </button>
        </div>
        <section className="panel-section">
          <label>
            Targets (required)
            <select
              multiple
              value={selectedTargets}
              onChange={handleSelectTargets}
            >
              {targets.map((target) => (
                <option key={target} value={target}>
                  {target}
                </option>
              ))}
            </select>
          </label>
          <label>
            Projects (optional)
            <select
              multiple
              value={selectedProjects}
              onChange={handleSelectProjects}
            >
              {projects.map((project) => (
                <option key={project.id} value={project.id}>
                  {project.name ? `${project.name} (${project.id})` : project.id}
                </option>
              ))}
            </select>
          </label>
        </section>
        <section className="panel-section">
          <div className="row">
            <label className="checkbox">
              <input
                type="checkbox"
                checked={forceBuild}
                onChange={(event) => setForceBuild(event.target.checked)}
              />
              Force
            </label>
            <label className="checkbox">
              <input
                type="checkbox"
                checked={retryBuild}
                onChange={(event) => setRetryBuild(event.target.checked)}
              />
              Retry
            </label>
          </div>
          <label>
            Parallelism
            <input
              type="number"
              min={1}
              value={parallelism}
              placeholder="auto"
              onChange={(event) => setParallelism(event.target.value)}
            />
          </label>
          <button
            className="primary"
            onClick={startBuild}
            disabled={buildRunning || selectedTargets.length === 0}
          >
            {buildRunning ? "Building..." : "Build"}
          </button>
          {buildError && <p className="error">{buildError}</p>}
        </section>
        <section className="panel-section details">
          <h2>Node Details</h2>
          {selectedNode ? (
            <div className="detail-card">
              <h3>{selectedNode.projectName ?? selectedNode.projectId}</h3>
              <p className="muted">{selectedNode.projectDir}</p>
              <div className="detail-row">
                <span>Target</span>
                <span>{selectedNode.target}</span>
              </div>
              {selectedResult ? (
                <>
                  <div className="detail-row">
                    <span>Status</span>
                    <span
                      className={
                        selectedResult.isSuccessful ? "ok" : "fail"
                      }
                    >
                      {selectedResult.isSuccessful ? "Success" : "Failed"}
                    </span>
                  </div>
                  <div className="detail-row">
                    <span>Duration</span>
                    <span>{selectedResult.duration}</span>
                  </div>
                  <div className="detail-row">
                    <span>Cache</span>
                    <span>{selectedResult.cache}</span>
                  </div>
                </>
              ) : (
                <p className="muted">
                  No cached result yet. Run a build or select another node.
                </p>
              )}
            </div>
          ) : (
            <p className="muted">Select a node in the graph to inspect it.</p>
          )}
        </section>
      </aside>
      <main className="workspace">
        <section className="graph">
          <header>
            <h2>Execution Graph</h2>
            {graphError && <span className="error">{graphError}</span>}
          </header>
          <div className="graph-canvas">
            {graph ? (
              <ReactFlow
                nodes={nodes}
                edges={edges}
                fitView
                onNodeClick={(_, node) =>
                  loadNodeResult(node.data.meta as GraphNode)
                }
              >
                <Background color="#31404e" gap={24} />
                <Controls position="bottom-right" />
              </ReactFlow>
            ) : (
              <div className="graph-empty">
                <p>Select at least one target to view the graph.</p>
              </div>
            )}
          </div>
        </section>
        <section className="terminal">
          <header>
            <h2>Build Log</h2>
            <span className={buildRunning ? "status live" : "status"}>
              {buildRunning ? "Live" : "Idle"}
            </span>
          </header>
          <div className="terminal-body" ref={terminalRef} />
        </section>
      </main>
    </div>
  );
};

export default App;
