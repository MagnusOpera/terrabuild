import { useEffect, useMemo, useRef, useState } from "react";
import {
  AppShell,
  Badge,
  Box,
  Button,
  Checkbox,
  Group,
  MultiSelect,
  Navbar,
  NumberInput,
  Paper,
  ActionIcon,
  Stack,
  Text,
  Title,
  useMantineColorScheme,
} from "@mantine/core";
import ReactFlow, { Background, Controls, Node, Edge } from "reactflow";
import "reactflow/dist/style.css";
import dagre from "dagre";
import { Terminal } from "xterm";
import { FitAddon } from "xterm-addon-fit";
import "xterm/css/xterm.css";
import { IconMoon, IconSun } from "@tabler/icons-react";

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
  const { colorScheme, toggleColorScheme } = useMantineColorScheme();

  const terminalRef = useRef<HTMLDivElement | null>(null);
  const terminal = useRef<Terminal | null>(null);
  const fitAddon = useRef<FitAddon | null>(null);
  const logAbort = useRef<AbortController | null>(null);

  useEffect(() => {
    const term = new Terminal({
      convertEol: false,
      scrollback: 3000,
      fontSize: 12,
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
    if (!terminal.current) {
      return;
    }
    terminal.current.options.theme = {
      background: colorScheme === "dark" ? "#141517" : "#ffffff",
      foreground: colorScheme === "dark" ? "#d8dbe0" : "#1f2328",
      selectionBackground: colorScheme === "dark" ? "#3b3f45" : "#c9d0d8",
    };
  }, [colorScheme]);

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
      style: {
        borderRadius: 12,
        border: "1px solid var(--mantine-color-gray-4)",
        padding: 8,
        fontSize: 12,
      },
    }));
    const flowEdges: Edge[] = rawNodes.flatMap((node) =>
      node.dependencies.map((dependency) => ({
        id: `${dependency}-${node.id}`,
        source: dependency,
        target: node.id,
        type: "smoothstep",
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
    <AppShell
      padding="md"
      navbar={
        <Navbar p="md" width={{ base: 360 }}>
          <Stack spacing="md">
            <Group position="apart" align="center">
              <Box>
                <Text size="xs" tt="uppercase" fw={600} c="dimmed">
                  Terrabuild
                </Text>
                <Title order={3}>Graph Console</Title>
              </Box>
              <ActionIcon
                onClick={() => toggleColorScheme()}
                variant="default"
                size="xl"
                radius="md"
                aria-label="Toggle color scheme"
              >
                {colorScheme === "dark" ? (
                  <IconSun stroke={1.5} />
                ) : (
                  <IconMoon stroke={1.5} />
                )}
              </ActionIcon>
            </Group>

            <MultiSelect
              data={targets.map((target) => ({ value: target, label: target }))}
              label="Targets (required)"
              placeholder="Select targets"
              searchable
              nothingFound="No targets"
              value={selectedTargets}
              onChange={(values) => setSelectedTargets(values)}
            />

            <MultiSelect
              data={projects.map((project) => ({
                value: project.id,
                label: project.name
                  ? `${project.name} (${project.id})`
                  : project.id,
              }))}
              label="Projects (optional)"
              placeholder="Select projects"
              searchable
              nothingFound="No projects"
              value={selectedProjects}
              onChange={(values) => setSelectedProjects(values)}
            />

            <Group spacing="md">
              <Checkbox
                label="Force"
                checked={forceBuild}
                onChange={(event) => setForceBuild(event.currentTarget.checked)}
              />
              <Checkbox
                label="Retry"
                checked={retryBuild}
                onChange={(event) => setRetryBuild(event.currentTarget.checked)}
              />
            </Group>

            <NumberInput
              label="Parallelism"
              placeholder="auto"
              min={1}
              value={parallelism === "" ? undefined : Number(parallelism)}
              onChange={(value) => {
                if (value === "" || value === null) {
                  setParallelism("");
                } else {
                  setParallelism(String(value));
                }
              }}
            />

            <Button
              onClick={startBuild}
              disabled={buildRunning || selectedTargets.length === 0}
            >
              {buildRunning ? "Building..." : "Build"}
            </Button>

            {buildError && (
              <Text size="sm" c="red">
                {buildError}
              </Text>
            )}

            <Paper withBorder p="md" radius="md">
              <Stack spacing="xs">
                <Text fw={600}>Node Details</Text>
                {selectedNode ? (
                  <>
                    <Text fw={600}>
                      {selectedNode.projectName ?? selectedNode.projectId}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {selectedNode.projectDir}
                    </Text>
                    <Group position="apart">
                      <Text size="sm" c="dimmed">
                        Target
                      </Text>
                      <Text size="sm">{selectedNode.target}</Text>
                    </Group>
                    {selectedResult ? (
                      <>
                        <Group position="apart">
                          <Text size="sm" c="dimmed">
                            Status
                          </Text>
                          <Badge color={selectedResult.isSuccessful ? "green" : "red"}>
                            {selectedResult.isSuccessful ? "Success" : "Failed"}
                          </Badge>
                        </Group>
                        <Group position="apart">
                          <Text size="sm" c="dimmed">
                            Duration
                          </Text>
                          <Text size="sm">{selectedResult.duration}</Text>
                        </Group>
                        <Group position="apart">
                          <Text size="sm" c="dimmed">
                            Cache
                          </Text>
                          <Text size="sm">{selectedResult.cache}</Text>
                        </Group>
                      </>
                    ) : (
                      <Text size="sm" c="dimmed">
                        No cached result yet.
                      </Text>
                    )}
                  </>
                ) : (
                  <Text size="sm" c="dimmed">
                    Select a node in the graph to inspect it.
                  </Text>
                )}
              </Stack>
            </Paper>
          </Stack>
        </Navbar>
      }
      styles={{ main: { height: "100vh" } }}
    >
      <Box style={{ height: "100%" }}>
        <Stack spacing="md" style={{ height: "100%" }}>
          <Paper
            withBorder
            radius="md"
            p="md"
            style={{ flex: 1, display: "flex", flexDirection: "column" }}
          >
            <Group position="apart" mb="sm">
              <Title order={4}>Execution Graph</Title>
              {graphError && (
                <Text size="sm" c="red">
                  {graphError}
                </Text>
              )}
            </Group>
            <Box style={{ flex: 1, minHeight: 0 }}>
              {graph ? (
                <ReactFlow
                  nodes={nodes}
                  edges={edges}
                  fitView
                  onNodeClick={(_, node) =>
                    loadNodeResult(node.data.meta as GraphNode)
                  }
                >
                  <Background gap={24} />
                  <Controls position="bottom-right" />
                </ReactFlow>
              ) : (
                <Group position="center" style={{ height: "100%" }}>
                  <Text size="sm" c="dimmed">
                    Select at least one target to view the graph.
                  </Text>
                </Group>
              )}
            </Box>
          </Paper>

          <Paper
            withBorder
            radius="md"
            p="md"
            style={{ height: 280, display: "flex", flexDirection: "column" }}
          >
            <Group position="apart" mb="sm">
              <Title order={4}>Build Log</Title>
              <Badge color={buildRunning ? "orange" : "gray"}>
                {buildRunning ? "Live" : "Idle"}
              </Badge>
            </Group>
            <Box className="terminal-body" ref={terminalRef} />
          </Paper>
        </Stack>
      </Box>
    </AppShell>
  );
};

export default App;
