import { useEffect, useMemo, useRef, useState } from "react";
import {
  Accordion,
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
  Select,
  Stack,
  Text,
  TextInput,
  Title,
  useMantineTheme,
  useMantineColorScheme,
} from "@mantine/core";
import { notifications } from "@mantine/notifications";
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
  MarkerType,
} from "reactflow";
import "reactflow/dist/style.css";
import dagre from "dagre";
import { Terminal } from "xterm";
import { FitAddon } from "xterm-addon-fit";
import "xterm/css/xterm.css";
import {
  IconAffiliate,
  IconCopy,
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
  rootNodes?: string[];
  engine?: string | null;
  configuration?: string | null;
  environment?: string | null;
};

type ProjectStatus = {
  projectId: string;
  status: "success" | "failed";
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

const engineOptions = [
  { value: "default", label: "Default" },
  { value: "none", label: "None" },
  { value: "docker", label: "Docker" },
  { value: "podman", label: "Podman" },
];

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
  const [engine, setEngine] = useState("default");
  const [configuration, setConfiguration] = useState("");
  const [environment, setEnvironment] = useState("");
  const [selectedProject, setSelectedProject] = useState<ProjectNode | null>(
    null
  );
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedTargetKey, setSelectedTargetKey] = useState<string | null>(
    null
  );
  const [projectStatus, setProjectStatus] = useState<
    Record<string, ProjectStatus["status"]>
  >({});
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
      term.write("\u001b[?25l");
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
        term.write("\u001b[?25h");
        term.dispose();
      };
    }
    const handleResize = () => fit.fit();
    window.addEventListener("resize", handleResize);
    return () => {
      window.removeEventListener("resize", handleResize);
      term.write("\u001b[?25h");
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
    const darkBackground = theme.colors.dark[7];
    const lightBackground = theme.white;
    terminal.current.options.theme = {
      background: colorScheme === "dark" ? darkBackground : lightBackground,
      foreground: colorScheme === "dark" ? "#d8dbe0" : "#1f2328",
      selectionBackground: colorScheme === "dark" ? "#3b3f45" : "#c9d0d8",
    };
  }, [colorScheme, theme]);

  const getNodeStyle = (nodeId: string) => {
    const isDark = colorScheme === "dark";
    const defaultBorder = isDark ? theme.colors.dark[3] : theme.colors.gray[6];
    const selectedBorder = theme.colors.blue[6];
    const nodeBackground = isDark ? theme.colors.dark[6] : theme.white;
    const nodeText = isDark ? theme.colors.gray[1] : theme.black;
    const status = projectStatus[nodeId];
    const statusColor =
      status === "failed"
        ? theme.fn.rgba(theme.colors.red[6], isDark ? 0.35 : 0.15)
        : status === "success"
        ? theme.fn.rgba(theme.colors.green[6], isDark ? 0.35 : 0.15)
        : null;
    return {
      borderRadius: 12,
      borderStyle: "solid",
      borderWidth: nodeId === selectedNodeId ? 2 : 1,
      borderColor: nodeId === selectedNodeId ? selectedBorder : defaultBorder,
      background: statusColor ?? nodeBackground,
      color: nodeText,
      padding: 8,
      fontSize: 32,
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      textAlign: "center",
      width: "fit-content",
      minHeight: 80,
      whiteSpace: "nowrap",
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
  }, [selectedNodeId, colorScheme, theme, projectStatus, setNodes]);

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

  const appendBuildParams = (params: URLSearchParams) => {
    const configValue = configuration.trim();
    const envValue = environment.trim();
    if (configValue.length > 0) {
      params.set("configuration", configValue);
    }
    if (envValue.length > 0) {
      params.set("environment", envValue);
    }
    if (engine !== "default") {
      params.set("engine", engine);
    }
  };

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
      appendBuildParams(params);
      const response = await fetch(`/api/graph?${params.toString()}`);
      if (!response.ok) {
        setGraphError(await response.text());
        setGraph(null);
        return;
      }
      const data = (await response.json()) as GraphResponse;
      setManualPositions({});
      setGraph(data);

      const statusResponse = await fetch(
        `/api/build/status?${params.toString()}`
      );
      if (statusResponse.ok) {
        const statusData = (await statusResponse.json()) as ProjectStatus[];
        const statusMap: Record<string, ProjectStatus["status"]> = {};
        statusData.forEach((item) => {
          statusMap[item.projectId] = item.status;
        });
        setProjectStatus(statusMap);
      } else {
        setProjectStatus({});
      }
    };
    fetchGraph().catch(() => {
      setGraphError("Failed to load graph.");
      setGraph(null);
    });
  }, [selectedTargets, selectedProjects, configuration, environment, engine]);

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
          markerStart: {
            type: MarkerType.ArrowClosed,
            color: edgeStroke,
            width: 52,
            height: 52,
          },
        });
      });
    });
    return layoutGraph(flowNodes, flowEdges);
  }, [graph, selectedNodeId, layoutVersion, colorScheme, theme]);

  const nodeCount = graph ? Object.keys(graph.nodes).length : 0;
  const rootNodeCount = graph?.rootNodes?.length ?? 0;
  const formatLabel = (value: string) =>
    value.length > 0 ? value[0].toUpperCase() + value.slice(1) : value;
  const engineValue = engine === "default" ? graph?.engine ?? "default" : engine;
  const configValue =
    configuration.trim().length > 0
      ? configuration.trim()
      : graph?.configuration ?? "default";
  const environmentValue =
    environment.trim().length > 0
      ? environment.trim()
      : graph?.environment ?? "default";
  const engineLabel = formatLabel(engineValue);
  const configurationLabel = formatLabel(configValue);
  const environmentLabel = formatLabel(environmentValue);

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
    const scrollTerminalToBottom = () => {
      terminal.current?.scrollToBottom();
    };
    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }
      if (value) {
        const chunk = decoder.decode(value, { stream: true });
        terminal.current.write(chunk);
        scrollTerminalToBottom();
      }
    }
    try {
      const params = new URLSearchParams();
      selectedTargets.forEach((target) => params.append("targets", target));
      selectedProjects.forEach((project) => params.append("projects", project));
      const statusResponse = await fetch(
        `/api/build/status?${params.toString()}`
      );
      if (statusResponse.ok) {
        const statusData = (await statusResponse.json()) as ProjectStatus[];
        const hasFailure = statusData.some((item) => item.status === "failed");
        notifications.show({
          color: hasFailure ? "red" : "green",
          title: hasFailure ? "Build completed with failures" : "Build completed",
          message: hasFailure
            ? "One or more targets failed."
            : "All targets completed successfully.",
        });
      } else {
        notifications.show({
          color: "yellow",
          title: "Build completed",
          message: "Unable to fetch build status.",
        });
      }
    } catch {
      notifications.show({
        color: "yellow",
        title: "Build completed",
        message: "Unable to fetch build status.",
      });
    }
    setBuildRunning(false);
  };

  const buildPayload = () => {
    const parallel =
      parallelism.trim().length > 0 ? Number(parallelism) : null;
    const configValue = configuration.trim();
    const envValue = environment.trim();
    const engineValue = engine === "default" ? undefined : engine;
    return {
      targets: selectedTargets,
      projects: selectedProjects,
      parallelism: parallel && parallel > 0 ? parallel : undefined,
      force: forceBuild,
      retry: retryBuild,
      configuration: configValue.length > 0 ? configValue : undefined,
      environment: envValue.length > 0 ? envValue : undefined,
      engine: engineValue,
    };
  };

  const copyTextToClipboard = async (value: string) => {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(value);
      return;
    }
    const textarea = document.createElement("textarea");
    textarea.value = value;
    textarea.style.position = "fixed";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand("copy");
    document.body.removeChild(textarea);
  };

  const copyBuildCommand = async () => {
    if (selectedTargets.length === 0) {
      return;
    }
    try {
      const response = await fetch("/api/build/command", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(buildPayload()),
      });
      if (!response.ok) {
        notifications.show({
          color: "red",
          title: "Copy failed",
          message: "Failed to generate build command.",
        });
        return;
      }
      const command = await response.text();
      await copyTextToClipboard(command);
      notifications.show({
        color: "green",
        title: "Copied",
        message: "Build command copied to clipboard.",
      });
    } catch {
      notifications.show({
        color: "red",
        title: "Copy failed",
        message: "Failed to copy build command.",
      });
    }
  };

  const startBuild = async () => {
    if (selectedTargets.length === 0 || buildRunning) {
      return;
    }
    setBuildRunning(true);
    setBuildError(null);
    const payload = buildPayload();
    const response = await fetch("/api/build", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!response.ok) {
      setBuildRunning(false);
      const message = await response.text();
      setBuildError(message);
      notifications.show({
        color: "red",
        title: "Build failed to start",
        message,
      });
      return;
    }
    notifications.show({
      color: "blue",
      title: "Build started",
      message: "Build is running.",
    });
    startLogStream().catch(() => {
      setBuildRunning(false);
      notifications.show({
        color: "red",
        title: "Build failed",
        message: "Build log stream failed.",
      });
    });
  };

  useEffect(() => {
    if (buildRunning) {
      return;
    }
    if (selectedTargets.length === 0) {
      return;
    }
    const refresh = async () => {
      const params = new URLSearchParams();
      selectedTargets.forEach((target) => params.append("targets", target));
      selectedProjects.forEach((project) => params.append("projects", project));
      appendBuildParams(params);
      const response = await fetch(`/api/graph?${params.toString()}`);
      if (response.ok) {
        const data = (await response.json()) as GraphResponse;
        setManualPositions({});
        setGraph(data);
      }
      const statusResponse = await fetch(
        `/api/build/status?${params.toString()}`
      );
      if (statusResponse.ok) {
        const statusData = (await statusResponse.json()) as ProjectStatus[];
        const statusMap: Record<string, ProjectStatus["status"]> = {};
        statusData.forEach((item) => {
          statusMap[item.projectId] = item.status;
        });
        setProjectStatus(statusMap);
      }
    };
    refresh().catch(() => null);
  }, [buildRunning, selectedTargets, selectedProjects, configuration, environment, engine]);

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
        terminal.current.scrollToBottom();
        return;
      }
      const log = await response.text();
      terminal.current.write(log.length > 0 ? log : "No cached log available.\n");
      terminal.current.scrollToBottom();
    } catch {
      terminal.current.write("Failed to load cached log.\n");
      terminal.current.scrollToBottom();
    }
  };

  return (
    <AppShell
      padding="md"
      navbar={
        <Navbar
          p="md"
          width={{ base: 360 }}
          style={{
            background:
              colorScheme === "dark"
                ? "rgba(20, 20, 23, 0.92)"
                : "rgba(250, 250, 250, 0.95)",
            display: "flex",
            flexDirection: "column",
            height: "100%",
          }}
        >
          <Box
            style={{
              flexShrink: 0,
              paddingBottom: theme.spacing.xs,
            }}
          >
            <Group position="apart" align="center">
              <Group spacing="sm" align="center">
                <Box
                  component="svg"
                  xmlns="http://www.w3.org/2000/svg"
                  width={40}
                  height={36}
                  viewBox="-450 -20 800 600"
                  aria-hidden="true"
                >
                  <defs>
                    <linearGradient
                      id="Gradient_1"
                      gradientUnits="userSpaceOnUse"
                      x1="818.3"
                      y1="493.056"
                      x2="1320.3"
                      y2="493.056"
                    >
                      <stop offset="0" stopColor="#FF0080" />
                      <stop offset="1" stopColor="#FF8000" />
                    </linearGradient>
                    <linearGradient
                      id="Gradient_2"
                      gradientUnits="userSpaceOnUse"
                      x1="561.3"
                      y1="486.056"
                      x2="1063.3"
                      y2="486.056"
                    >
                      <stop offset="0" stopColor="#7A7ECF" />
                      <stop offset="1" stopColor="#00BBFF" />
                    </linearGradient>
                  </defs>
                  <g id="Layer_1" transform="translate(-1000, -206.057)">
                    <path
                      d="M1285.1,426.55 L959.85,226.6 C899.51,189.51 822.14,232.94 821.53,304.25 L821.28,333.21 L861.81,333.21 C870.58,286.46 926.09,260.4 970.14,286.42 L1227.66,438.53 C1264.82,460.97 1264.82,513.44 1227.66,535.88 L970.46,691.22 C922.46,720.21 860.72,687.98 858.16,634.21 L818.69,634.21 L818.31,678.35 C817.67,752.27 898.1,797.82 960.27,758.74 L1285.11,554.53 C1332.03,525.02 1332.03,456.06 1285.1,426.55 z"
                      fill="url(#Gradient_1)"
                    />
                    <path
                      d="M1028.1,419.55 L702.85,219.6 C642.51,182.51 565.14,225.94 564.53,297.25 L561.31,671.35 C560.67,745.27 641.1,790.82 703.27,751.74 L1028.11,547.53 C1075.03,518.02 1075.03,449.06 1028.1,419.55 z M973.1,532.82 L720.09,688.16 C671.67,717.89 609.03,683.24 609.52,627.01 L612.03,342.43 C612.51,288.19 672.77,255.14 719.77,283.36 L973.1,435.47 C1009.66,457.91 1009.66,510.37 973.1,532.82 z"
                      fill="url(#Gradient_2)"
                    />
                  </g>
                </Box>
                <Box>
                  <Text size="xs" tt="uppercase" fw={600} c="dimmed">
                    Terrabuild
                  </Text>
                  <Title order={3}>Graph Console</Title>
                </Box>
              </Group>
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
          </Box>

          <Box style={{ flex: 1, overflowY: "auto", marginTop: theme.spacing.xs }}>
            <Stack spacing="md">

            <Paper withBorder p="md" radius="md" shadow="md">
              <Stack spacing="sm">
                <MultiSelect
                  data={targets.map((target) => ({ value: target, label: target }))}
                  label="Targets"
                  placeholder="Select targets"
                  searchable
                  nothingFound="No targets"
                  value={selectedTargets}
                  onChange={(values) => setSelectedTargets(values)}
                />

                <Group spacing="md">
                  <Checkbox
                    label="Force"
                    checked={forceBuild}
                    onChange={(event) => {
                      const checked = event.currentTarget.checked;
                      setForceBuild(checked);
                      if (checked) {
                        setRetryBuild(false);
                      }
                    }}
                  />
                  <Checkbox
                    label="Retry"
                    checked={retryBuild}
                    onChange={(event) => {
                      const checked = event.currentTarget.checked;
                      setRetryBuild(checked);
                      if (checked) {
                        setForceBuild(false);
                      }
                    }}
                  />
                </Group>

                <Accordion
                  variant="contained"
                  radius={0}
                  defaultValue={null}
                  style={{ width: "100%", marginLeft: 0, marginRight: 0 }}
                  styles={{
                    item: {
                      borderLeft: "none",
                      borderRight: "none",
                      borderTop: "none",
                      borderBottom: "none",
                    },
                    control: {
                      paddingLeft: 0,
                      paddingRight: 0,
                      backgroundColor:
                        colorScheme === "dark"
                          ? theme.colors.dark[7]
                          : theme.white,
                    },
                    content: { paddingLeft: 0, paddingRight: 0 },
                    panel: {
                      paddingLeft: 0,
                      paddingRight: 0,
                      backgroundColor:
                        colorScheme === "dark"
                          ? theme.colors.dark[7]
                          : theme.white,
                    },
                  }}
                >
                  <Accordion.Item value="advanced">
                    <Accordion.Control>Advanced</Accordion.Control>
                    <Accordion.Panel>
                      <Stack spacing="sm">
                        <MultiSelect
                          data={projects
                            .filter((project) => project.name)
                            .map((project) => ({
                              value: project.name as string,
                              label: project.name as string,
                            }))}
                          label="Projects"
                          placeholder="Select projects"
                          searchable
                          nothingFound="No projects"
                          value={selectedProjects}
                          onChange={(values) => setSelectedProjects(values)}
                        />

                        <TextInput
                          label="Configuration"
                          placeholder="default"
                          value={configuration}
                          onChange={(event) =>
                            setConfiguration(event.currentTarget.value)
                          }
                        />

                        <TextInput
                          label="Environment"
                          placeholder="default"
                          value={environment}
                          onChange={(event) =>
                            setEnvironment(event.currentTarget.value)
                          }
                        />

                        <Select
                          data={engineOptions}
                          label="Engine"
                          value={engine}
                          onChange={(value) => setEngine(value ?? "default")}
                        />

                        <NumberInput
                          label="Parallelism"
                          placeholder="auto"
                          min={1}
                          value={
                            parallelism === "" ? undefined : Number(parallelism)
                          }
                          onChange={(value) => {
                            if (value === "" || value === null) {
                              setParallelism("");
                            } else {
                              setParallelism(String(value));
                            }
                          }}
                        />
                      </Stack>
                    </Accordion.Panel>
                  </Accordion.Item>
                </Accordion>

                <Group spacing="xs" noWrap>
                  <Button
                    onClick={startBuild}
                    disabled={buildRunning || selectedTargets.length === 0}
                    style={{ flex: 1 }}
                  >
                    {buildRunning ? "Building..." : "Build"}
                  </Button>
                  <ActionIcon
                    size="lg"
                    variant="light"
                    onClick={copyBuildCommand}
                    disabled={selectedTargets.length === 0}
                    aria-label="Copy build command"
                  >
                    <IconCopy size={18} />
                  </ActionIcon>
                </Group>

                {buildError && (
                  <Text size="sm" c="red">
                    {buildError}
                  </Text>
                )}

              </Stack>
            </Paper>

            <Paper withBorder p="md" radius="md" shadow="md">
              <Stack spacing="xs">
                <Text fw={600}>Build Details</Text>
                {graph ? (
                  <>
                    <Group position="apart">
                      <Text size="sm" c="dimmed">
                        Nodes
                      </Text>
                      <Text size="sm" fw={600}>
                        {nodeCount}
                      </Text>
                    </Group>
                    <Group position="apart">
                      <Text size="sm" c="dimmed">
                        Root nodes
                      </Text>
                      <Text size="sm" fw={600}>
                        {rootNodeCount}
                      </Text>
                    </Group>
                    <Group position="apart">
                      <Text size="sm" c="dimmed">
                        Configuration
                      </Text>
                      <Text size="sm" fw={600}>
                        {configurationLabel}
                      </Text>
                    </Group>
                    <Group position="apart">
                      <Text size="sm" c="dimmed">
                        Environment
                      </Text>
                      <Text size="sm" fw={600}>
                        {environmentLabel}
                      </Text>
                    </Group>
                    <Group position="apart">
                      <Text size="sm" c="dimmed">
                        Engine
                      </Text>
                      <Text size="sm" fw={600}>
                        {engineLabel}
                      </Text>
                    </Group>
                  </>
                ) : (
                  <Text size="sm" c="dimmed">
                    Select targets to load build details.
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
          </Box>
        </Navbar>
      }
      styles={{ main: { height: "100vh" } }}
    >
      <Box style={{ height: "100%" }}>
        <Stack spacing={showTerminal ? "md" : 0} style={{ height: "100%" }}>
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
                    proOptions={{ hideAttribution: true }}
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
            withBorder={showTerminal}
            shadow={showTerminal ? "md" : undefined}
            radius="md"
            p={showTerminal ? "md" : 0}
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
            {showTerminal && (
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
            )}
            <Box
              className="terminal-body"
              ref={terminalRef}
              style={{
                background:
                  colorScheme === "dark" ? theme.colors.dark[7] : theme.white,
              }}
            />
          </Paper>
        </Stack>
      </Box>
    </AppShell>
  );
};

export default App;
