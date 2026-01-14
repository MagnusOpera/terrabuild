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
  useMantineTheme,
  useMantineColorScheme,
} from "@mantine/core";
import ReactFlow, {
  Background,
  Controls,
  Node,
  Edge,
  Position,
  applyNodeChanges,
  useEdgesState,
  useNodesState,
  ReactFlowInstance,
} from "reactflow";
import "reactflow/dist/style.css";
import dagre from "dagre";
import { Terminal } from "xterm";
import { FitAddon } from "xterm-addon-fit";
import "xterm/css/xterm.css";
import {
  IconAffiliate,
  IconMoon,
  IconSun,
  IconSquareRoundedChevronDown,
} from "@tabler/icons-react";

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

type ProjectNode = {
  id: string;
  name?: string | null;
  directory: string;
  hash: string;
  targets: GraphNode[];
};

type OperationSummary = {
  metaCommand: string;
  command: string;
  arguments: string;
  log: string;
  exitCode: number;
};

type TargetSummary = {
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

const nodeWidth = 320;
const nodeHeight = 120;

const layoutGraph = (nodes: Node[], edges: Edge[]) => {
  const graph = new dagre.graphlib.Graph();
  graph.setDefaultEdgeLabel(() => ({}));
  graph.setGraph({ rankdir: "LR", nodesep: 90, ranksep: 140 });

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
  const [selectedProject, setSelectedProject] = useState<ProjectNode | null>(
    null
  );
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedTargetKey, setSelectedTargetKey] = useState<string | null>(
    null
  );
  const [showTerminal, setShowTerminal] = useState(false);
  const [nodeResults, setNodeResults] = useState<Record<string, TargetSummary>>(
    {}
  );
  const [layoutVersion, setLayoutVersion] = useState(0);
  const [manualPositions, setManualPositions] = useState<
    Record<string, { x: number; y: number }>
  >({});
  const [nodes, setNodes] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const flowInstance = useRef<ReactFlowInstance | null>(null);
  const { colorScheme, toggleColorScheme } = useMantineColorScheme();
  const theme = useMantineTheme();

  const terminalRef = useRef<HTMLDivElement | null>(null);
  const terminal = useRef<Terminal | null>(null);
  const fitAddon = useRef<FitAddon | null>(null);
  const logAbort = useRef<AbortController | null>(null);
  const terminalReady = useRef(false);

  useEffect(() => {
    const term = new Terminal({
      convertEol: true,
      scrollback: 3000,
      fontSize: 12,
    });
    const fit = new FitAddon();
    term.loadAddon(fit);
    terminal.current = term;
    fitAddon.current = fit;
    if (terminalRef.current) {
      term.open(terminalRef.current);
      terminalReady.current = true;
      const resizeObserver = new ResizeObserver(() => {
        if (!terminalRef.current) {
          return;
        }
        if (terminalRef.current.offsetWidth === 0) {
          return;
        }
        fit.fit();
      });
      resizeObserver.observe(terminalRef.current);
      requestAnimationFrame(() => {
        if (!terminalRef.current) {
          return;
        }
        if (terminalRef.current.offsetWidth === 0) {
          return;
        }
        fit.fit();
      });
      return () => {
        resizeObserver.disconnect();
        term.dispose();
      };
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
    if (!terminalReady.current) {
      return;
    }
    if (!terminalRef.current || terminalRef.current.offsetWidth === 0) {
      return;
    }
    terminal.current.options.theme = {
      background: colorScheme === "dark" ? "#141517" : "#ffffff",
      foreground: colorScheme === "dark" ? "#d8dbe0" : "#1f2328",
      selectionBackground: colorScheme === "dark" ? "#3b3f45" : "#c9d0d8",
    };
  }, [colorScheme]);

  const getNodeStyle = (nodeId: string) => {
    const isDark = colorScheme === "dark";
    const defaultBorder = isDark ? theme.colors.dark[3] : theme.colors.gray[6];
    const selectedBorder = theme.colors.blue[6];
    const nodeBackground = isDark ? theme.colors.dark[6] : theme.white;
    const nodeText = isDark ? theme.colors.gray[1] : theme.black;
    return {
      borderRadius: 12,
      borderStyle: "solid",
      borderWidth: nodeId === selectedNodeId ? 2 : 1,
      borderColor: nodeId === selectedNodeId ? selectedBorder : defaultBorder,
      background: nodeBackground,
      color: nodeText,
      padding: 8,
      fontSize: 32,
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      width: "fit-content",
      minWidth: 200,
      minHeight: 80,
      maxWidth: 360,
      boxShadow:
        nodeId === selectedNodeId
          ? "0 0 0 2px rgba(34, 139, 230, 0.2)"
          : "none",
    };
  };

  useEffect(() => {
    const selected = nodes.find((node) => node.selected);
    setSelectedNodeId(selected ? selected.id : null);
  }, [nodes]);

  useEffect(() => {
    setNodes((current) =>
      current.map((node) => ({
        ...node,
        style: getNodeStyle(node.id),
      }))
    );
  }, [selectedNodeId, colorScheme, theme, setNodes]);

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
      setManualPositions({});
      setGraph(data);
    };
    fetchGraph().catch(() => {
      setGraphError("Failed to load graph.");
      setGraph(null);
    });
  }, [selectedTargets, selectedProjects]);

  const baseGraph = useMemo(() => {
    if (!graph) {
      return { nodes: [], edges: [] };
    }
    const projectMap = new Map<string, ProjectNode>();
    const nodeMap = new Map<string, GraphNode>();
    Object.values(graph.nodes).forEach((node) => {
      nodeMap.set(node.id, node);
      const existing = projectMap.get(node.projectId);
      if (existing) {
        existing.targets.push(node);
      } else {
        projectMap.set(node.projectId, {
          id: node.projectId,
          name: node.projectName,
          directory: node.projectDir,
          hash: node.projectHash,
          targets: [node],
        });
      }
    });

    const isDark = colorScheme === "dark";
    const edgeStroke = isDark ? theme.colors.dark[3] : theme.colors.gray[5];

    const flowNodes: Node[] = Array.from(projectMap.values())
      .filter((project) => project.directory !== ".")
      .map((project) => ({
        id: project.id,
        data: {
          label: project.directory,
          meta: project,
        },
        position: { x: 0, y: 0 },
        sourcePosition: Position.Right,
        targetPosition: Position.Left,
        style: getNodeStyle(project.id),
      }));

    const visibleProjects = new Set(flowNodes.map((node) => node.id));
    const edgeSet = new Set<string>();
    const flowEdges: Edge[] = [];
    nodeMap.forEach((node) => {
      node.dependencies.forEach((dependency) => {
        const depNode = nodeMap.get(dependency);
        if (!depNode) {
          return;
        }
        if (depNode.projectId === node.projectId) {
          return;
        }
        if (
          !visibleProjects.has(depNode.projectId) ||
          !visibleProjects.has(node.projectId)
        ) {
          return;
        }
        const edgeId = `${depNode.projectId}->${node.projectId}`;
        if (edgeSet.has(edgeId)) {
          return;
        }
        edgeSet.add(edgeId);
        flowEdges.push({
          id: edgeId,
          source: depNode.projectId,
          target: node.projectId,
          type: "default",
          style: { stroke: edgeStroke },
        });
      });
    });
    return layoutGraph(flowNodes, flowEdges);
  }, [graph, selectedNodeId, layoutVersion, colorScheme, theme]);

  useEffect(() => {
    if (!graph) {
      setNodes([]);
      setEdges([]);
      return;
    }
    const manualKeys = Object.keys(manualPositions);
    if (manualKeys.length > 0) {
      setNodes((current) =>
        current.map((node) => ({
          ...node,
          position: manualPositions[node.id] ?? node.position,
        }))
      );
      return;
    }
    setNodes(baseGraph.nodes);
    setEdges(baseGraph.edges);
    requestAnimationFrame(() => {
      flowInstance.current?.fitView({
        padding: 0.5,
        duration: 300,
        minZoom: 0.1,
      });
    });
  }, [graph, baseGraph, manualPositions, setNodes, setEdges]);

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
    setShowTerminal(true);
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
      projects: selectedProjects,
      parallelism: parallel && parallel > 0 ? parallel : undefined,
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

  const loadProjectResults = async (project: ProjectNode) => {
    setSelectedProject(project);
    setSelectedNodeId(project.id);
    setSelectedTargetKey(null);
    await Promise.all(
      project.targets.map(async (node) => {
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
      })
    );
    if (project.targets.length > 0) {
      const first = project.targets[0];
      const cacheKey = `${first.projectHash}/${first.target}/${first.targetHash}`;
      await showTargetLog(cacheKey, first);
    }
  };

  const buildTargetLog = (summary: TargetSummary) => {
    return summary.operations
      .flatMap((group) =>
        group.map((operation) => {
          const header = operation.metaCommand || operation.command;
          return `${header}\n${operation.log || ""}`.trim();
        })
      )
      .filter((value) => value.length > 0)
      .join("\n\n");
  };

  const showTargetLog = async (key: string, target: GraphNode) => {
    if (!terminal.current) {
      return;
    }
    setSelectedTargetKey(key);
    terminal.current.reset();
    setShowTerminal(true);
    try {
      const response = await fetch(
        `/api/build/target-log/${target.projectHash}/${target.target}/${target.targetHash}`
      );
      if (!response.ok) {
        terminal.current.write("No cached log available.\n");
        return;
      }
      const log = await response.text();
      terminal.current.write(log.length > 0 ? log : "No cached log available.\n");
    } catch {
      terminal.current.write("Failed to load cached log.\n");
    }
  };

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
              </Box>
              <ActionIcon
                onClick={() => toggleColorScheme()}
                variant="light"
                size="lg"
                aria-label="Toggle color scheme"
              >
                {colorScheme === "dark" ? (
                  <IconSun size={18} />
                ) : (
                  <IconMoon size={18} />
                )}
              </ActionIcon>
            </Group>

            <Paper withBorder p="md" radius="md" shadow="md">
              <Stack spacing="sm">
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
              </Stack>
            </Paper>

            <Paper withBorder p="md" radius="md" shadow="md">
              <Stack spacing="xs">
                <Text fw={600}>Node Details</Text>
                {selectedProject ? (
                  <>
                    <Text fw={600}>{selectedProject.directory}</Text>
                    <Stack spacing="xs">
                      {selectedProject.targets.map((target) => {
                        const cacheKey = `${target.projectHash}/${target.target}/${target.targetHash}`;
                        const summary = nodeResults[cacheKey];
                        return (
                          <Button
                            key={cacheKey}
                            variant={
                              selectedTargetKey === cacheKey
                                ? "filled"
                                : "light"
                            }
                            color={
                              selectedTargetKey === cacheKey
                                ? "blue"
                                : "gray"
                            }
                            onClick={() =>
                              showTargetLog(cacheKey, target)
                            }
                            rightIcon={
                              summary ? (
                                <Badge
                                  color={summary.isSuccessful ? "green" : "red"}
                                >
                                  {summary.isSuccessful ? "Success" : "Failed"}
                                </Badge>
                              ) : (
                                <Badge color="gray">No cache</Badge>
                              )
                            }
                          >
                            {target.target}
                          </Button>
                        );
                      })}
                    </Stack>
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
            shadow="md"
            radius="md"
            p="md"
            style={{ flex: 1, display: "flex", flexDirection: "column", minHeight: 0 }}
          >
            <Group position="apart" mb="sm">
              <Title order={4}>Execution Graph</Title>
              <Group spacing="xs">
                {graphError && (
                  <Text size="sm" c="red">
                    {graphError}
                  </Text>
                )}
                <ActionIcon
                  size="lg"
                  variant="light"
                  onClick={() => {
                    setManualPositions({});
                    setLayoutVersion((value) => value + 1);
                  }}
                  aria-label="Reflow graph"
                >
                  <IconAffiliate size={18} />
                </ActionIcon>
              </Group>
            </Group>
            <Box style={{ flex: 1, minHeight: 0, width: "100%", height: "100%" }}>
              {graph ? (
                  <ReactFlow
                    nodes={nodes}
                    edges={edges}
                    fitView
                    fitViewOptions={{ padding: 0.5, minZoom: 0.1 }}
                    minZoom={0.1}
                    onInit={(instance) => {
                      flowInstance.current = instance;
                    }}
                    nodesDraggable
                    elementsSelectable
                    panOnDrag
                    onNodesChange={(changes) => {
                      setNodes((current) => {
                        const updated = applyNodeChanges(changes, current);
                        const positions: Record<string, { x: number; y: number }> =
                          {};
                        updated.forEach((node) => {
                          positions[node.id] = node.position;
                          if (node.selected) {
                            setSelectedNodeId(node.id);
                          }
                        });
                        setManualPositions(positions);
                        return updated;
                      });
                    }}
                    onEdgesChange={onEdgesChange}
                    onNodeClick={(_, node) =>
                      loadProjectResults(node.data.meta as ProjectNode)
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
            shadow="md"
            radius="md"
            p="md"
            style={{
              height: showTerminal ? 280 : 0,
              opacity: showTerminal ? 1 : 0,
              display: "flex",
              flexDirection: "column",
              overflow: "hidden",
              transition: "height 200ms ease, opacity 200ms ease",
              pointerEvents: showTerminal ? "auto" : "none",
            }}
          >
            <Group position="apart" mb="sm">
              <Title order={4}>Build Log</Title>
              <Group spacing="xs">
                <Badge color={buildRunning ? "orange" : "gray"}>
                  {buildRunning ? "Live" : "Idle"}
                </Badge>
                <ActionIcon
                  size="lg"
                  variant="subtle"
                  onClick={() => setShowTerminal(false)}
                  aria-label="Hide terminal"
                >
                  <IconSquareRoundedChevronDown size={18} />
                </ActionIcon>
              </Group>
            </Group>
            <Box className="terminal-body" ref={terminalRef} />
          </Paper>
        </Stack>
      </Box>
    </AppShell>
  );
};

export default App;
