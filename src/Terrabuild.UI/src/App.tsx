import { useEffect, useMemo, useRef, useState } from "react";
import {
  Box,
  Stack,
  rgba,
  useMantineColorScheme,
  useMantineTheme,
} from "@mantine/core";
import { useMediaQuery } from "@mantine/hooks";
import { notifications } from "@mantine/notifications";
import {
  Edge,
  MarkerType,
  Node,
  OnNodesChange,
  Position,
  ReactFlowInstance,
  applyNodeChanges,
  useEdgesState,
  useNodesState,
} from "reactflow";
import "reactflow/dist/style.css";
import dagre from "@dagrejs/dagre";
import { Terminal } from "xterm";
import { FitAddon } from "xterm-addon-fit";
import "xterm/css/xterm.css";
import BuildControlsPanel from "./components/BuildControlsPanel";
import BuildDetailsPanel from "./components/BuildDetailsPanel";
import BuildLogPanel from "./components/BuildLogPanel";
import GraphPanel from "./components/GraphPanel";
import NodeDetailsPanel from "./components/NodeDetailsPanel";
import SidebarLayout from "./components/SidebarLayout";
import SidebarHeader from "./components/SidebarHeader";
import {
  GraphNode,
  GraphResponse,
  ProjectInfo,
  ProjectNode,
  ProjectStatus,
  TargetSummary,
} from "./types";

type ProjectStatusMap = Record<string, ProjectStatus["status"]>;

const nodeWidth = 320;
const nodeHeight = 120;

const layoutGraph = (nodes: Node[], edges: Edge[]) => {
  const graph = new dagre.graphlib.Graph();
  graph.setDefaultEdgeLabel(() => ({}));
  graph.setGraph({ rankdir: "RL", nodesep: 100, ranksep: 200, ranker: "longest-path" });

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
  const [logBuild, setLogBuild] = useState(false);
  const [debugBuild, setDebugBuild] = useState(false);
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
  const [projectStatus, setProjectStatus] = useState<ProjectStatusMap>({});
  const [showTerminal, setShowTerminal] = useState(false);
  const [nodeResults, setNodeResults] = useState<Record<string, TargetSummary>>(
    {}
  );
  const [layoutVersion, setLayoutVersion] = useState(0);
  const [manualPositions, setManualPositions] = useState<
    Record<string, { x: number; y: number }>
  >({});
  const [draggedNodeId, setDraggedNodeId] = useState<string | null>(null);
  const [buildEndedAt, setBuildEndedAt] = useState<Date | null>(null);
  const [nodes, setNodes] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  const flowInstance = useRef<ReactFlowInstance | null>(null);
  const { colorScheme } = useMantineColorScheme();
  const theme = useMantineTheme();
  const prefersDark = useMediaQuery("(prefers-color-scheme: dark)");
  const effectiveColorScheme =
    colorScheme === "auto" ? (prefersDark ? "dark" : "light") : colorScheme;

  const terminalRef = useRef<HTMLDivElement | null>(null);
  const terminal = useRef<Terminal | null>(null);
  const fitAddon = useRef<FitAddon | null>(null);
  const logAbort = useRef<AbortController | null>(null);
  const terminalReady = useRef(false);
  const pendingTargetRef = useRef<{ key: string; target: GraphNode } | null>(
    null
  );
  const pendingBuildLogRef = useRef(false);
  const lastApiNoticeRef = useRef(0);
  const applyTerminalTheme = () => {
    if (!terminal.current || !terminalReady.current) {
      return;
    }
    if (!terminalRef.current || terminalRef.current.offsetWidth === 0) {
      return;
    }
    const darkBackground = theme.colors.dark[7];
    const lightBackground = theme.white;
    terminal.current.options.theme = {
      background:
        effectiveColorScheme === "dark" ? darkBackground : lightBackground,
      foreground: effectiveColorScheme === "dark" ? "#d8dbe0" : "#1f2328",
      selectionBackground:
        effectiveColorScheme === "dark" ? "#3b3f45" : "#c9d0d8",
    };
  };

  const notifyApiUnavailable = () => {
    const now = Date.now();
    if (now - lastApiNoticeRef.current < 15000) {
      return;
    }
    lastApiNoticeRef.current = now;
    notifications.show({
      color: "red",
      title: "API unavailable",
      message: "Unable to reach the Terrabuild API.",
    });
  };
  const flushPendingTerminalActions = () => {
    if (!terminalReady.current) {
      return;
    }
    if (pendingTargetRef.current) {
      const pending = pendingTargetRef.current;
      pendingTargetRef.current = null;
      void loadTargetLog(pending.key, pending.target);
    }
    if (pendingBuildLogRef.current) {
      pendingBuildLogRef.current = false;
      void startLogStreamInternal();
    }
  };

  useEffect(() => {
    if (!showTerminal) {
      return;
    }
    if (!terminalRef.current || terminal.current) {
      return;
    }
    const term = new Terminal({
      convertEol: true,
      scrollback: 3000,
      fontSize: 12,
    });
    const fit = new FitAddon();
    term.loadAddon(fit);
    terminal.current = term;
    fitAddon.current = fit;
    term.open(terminalRef.current);
    term.write("\u001b[?25l");
    terminalReady.current = true;
    applyTerminalTheme();
    flushPendingTerminalActions();
    const resizeObserver = new ResizeObserver(() => {
      if (!terminalRef.current) {
        return;
      }
      if (
        terminalRef.current.offsetWidth === 0 ||
        terminalRef.current.offsetHeight === 0
      ) {
        return;
      }
      fit.fit();
    });
    resizeObserver.observe(terminalRef.current);
    requestAnimationFrame(() => {
      if (!terminalRef.current) {
        return;
      }
      if (
        terminalRef.current.offsetWidth === 0 ||
        terminalRef.current.offsetHeight === 0
      ) {
        return;
      }
      fit.fit();
      applyTerminalTheme();
    });
    const handleResize = () => {
      if (
        !terminalRef.current ||
        terminalRef.current.offsetWidth === 0 ||
        terminalRef.current.offsetHeight === 0
      ) {
        return;
      }
      fit.fit();
    };
    window.addEventListener("resize", handleResize);
    return () => {
      window.removeEventListener("resize", handleResize);
      resizeObserver.disconnect();
      terminalReady.current = false;
      term.write("\u001b[?25h");
      term.dispose();
      terminal.current = null;
      fitAddon.current = null;
    };
  }, [showTerminal]);

  useEffect(() => {
    applyTerminalTheme();
  }, [effectiveColorScheme, theme]);

  const getNodeStyle = (nodeId: string, isNamedProject: boolean) => {
    const isDark = effectiveColorScheme === "dark";
    const defaultBorder = isDark ? theme.colors.dark[3] : theme.colors.gray[6];
    const selectedBorder = theme.colors.blue[6];
    const nodeBackground = isDark ? theme.colors.dark[6] : theme.white;
    const nodeText = isDark ? theme.colors.gray[1] : theme.black;
    const status = projectStatus[nodeId];
    const statusColor =
      status === "failed"
        ? rgba(theme.colors.red[6], isDark ? 0.35 : 0.15)
        : status === "success"
        ? rgba(theme.colors.green[6], isDark ? 0.35 : 0.15)
        : null;
    return {
      borderRadius: isNamedProject ? 9999 : 12,
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
    if (selectedNodeId === null) {
      setSelectedProject(null);
      setSelectedTargetKey(null);
    }
  }, [selectedNodeId]);

  useEffect(() => {
    setNodes((current) =>
      current.map((node) => ({
        ...node,
        style: getNodeStyle(
          node.id,
          Boolean((node.data as { meta?: ProjectNode })?.meta?.name)
        ),
      }))
    );
  }, [selectedNodeId, effectiveColorScheme, theme, projectStatus, setNodes]);

  useEffect(() => {
    const load = async () => {
      try {
        const [targetsRes, projectsRes] = await Promise.all([
          fetch("/api/targets"),
          fetch("/api/projects"),
        ]);
        if (targetsRes.ok) {
          setTargets(await targetsRes.json());
        } else {
          notifyApiUnavailable();
        }
        if (projectsRes.ok) {
          setProjects(await projectsRes.json());
        } else {
          notifyApiUnavailable();
        }
      } catch {
        notifyApiUnavailable();
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
        const statusMap: ProjectStatusMap = {};
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
      notifyApiUnavailable();
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

    const isDark = effectiveColorScheme === "dark";
    const edgeStroke = isDark ? theme.colors.dark[3] : theme.colors.gray[5];

    const flowNodes: Node[] = Array.from(projectMap.values())
      .filter((project) => project.directory !== ".")
      .map((project) => {
        const projectName = project.name?.trim();
        const hasProjectName = Boolean(projectName);
        return {
          id: project.id,
          data: {
            label: hasProjectName ? projectName : project.directory,
            meta: project,
          },
          position: { x: 0, y: 0 },
          sourcePosition: Position.Left,
          targetPosition: Position.Right,
          style: getNodeStyle(project.id, hasProjectName),
        };
      });

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
  }, [graph, selectedNodeId, layoutVersion, effectiveColorScheme, theme]);

  useEffect(() => {
    const isDark = effectiveColorScheme === "dark";
    const defaultStroke = isDark ? theme.colors.dark[3] : theme.colors.gray[5];
    const highlightStroke = theme.colors.blue[6];
    setEdges((current) =>
      current.map((edge) => {
        const isConnected =
          (draggedNodeId !== null &&
            (edge.source === draggedNodeId || edge.target === draggedNodeId)) ||
          (selectedNodeId !== null &&
            (edge.source === selectedNodeId || edge.target === selectedNodeId));
        const stroke = isConnected ? highlightStroke : defaultStroke;
        return {
          ...edge,
          style: {
            ...edge.style,
            stroke,
            strokeWidth: isConnected ? 2 : 1,
          },
          markerStart: edge.markerStart
            ? {
                ...edge.markerStart,
                color: stroke,
              }
            : edge.markerStart,
        };
      })
    );
  }, [draggedNodeId, selectedNodeId, effectiveColorScheme, theme, setEdges]);

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

  const startLogStreamInternal = async () => {
    if (!terminal.current) {
      return;
    }
    terminal.current.reset();
    setShowTerminal(true);
    logAbort.current?.abort();
    const controller = new AbortController();
    logAbort.current = controller;

    let response: Response;
    try {
      response = await fetch("/api/build/log", { signal: controller.signal });
    } catch {
      notifyApiUnavailable();
      setBuildRunning(false);
      return;
    }
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
  
  const startLogStream = async () => {
    if (!terminal.current) {
      pendingBuildLogRef.current = true;
      setShowTerminal(true);
      return;
    }
    await startLogStreamInternal();
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
      log: logBuild ? true : undefined,
      debug: debugBuild ? true : undefined,
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
      notifyApiUnavailable();
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
    let response: Response;
    try {
      response = await fetch("/api/build", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
    } catch {
      setBuildRunning(false);
      setBuildEndedAt(null);
      notifyApiUnavailable();
      return;
    }
    if (!response.ok) {
      setBuildRunning(false);
      setBuildEndedAt(null);
      const message = await response.text();
      setBuildError(message);
      notifications.show({
        color: "red",
        title: "Build failed to start",
        message,
      });
      return;
    }
    setBuildEndedAt(null);
    notifications.show({
      color: "blue",
      title: "Build started",
      message: "Build is running.",
    });
    startLogStream().catch(() => {
      setBuildRunning(false);
      notifyApiUnavailable();
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
        const statusMap: ProjectStatusMap = {};
        statusData.forEach((item) => {
          statusMap[item.projectId] = item.status;
        });
        setProjectStatus(statusMap);
      }
    };
    refresh().catch(() => {
      notifyApiUnavailable();
    });
  }, [
    buildRunning,
    selectedTargets,
    selectedProjects,
    configuration,
    environment,
    engine,
  ]);

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

  const loadTargetLog = async (key: string, target: GraphNode) => {
    if (!terminal.current) {
      return;
    }
    const summary = nodeResults[key];
    if (summary?.endedAt) {
      setBuildEndedAt(new Date(summary.endedAt));
    } else {
      setBuildEndedAt(null);
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

  const showTargetLog = async (key: string, target: GraphNode) => {
    if (!terminal.current) {
      pendingTargetRef.current = { key, target };
      setShowTerminal(true);
      return;
    }
    await loadTargetLog(key, target);
  };

  useEffect(() => {
    if (!selectedTargetKey) {
      return;
    }
    const summary = nodeResults[selectedTargetKey];
    if (summary?.endedAt) {
      setBuildEndedAt(new Date(summary.endedAt));
    }
  }, [selectedTargetKey, nodeResults]);

  const handleNodesChange: OnNodesChange = (changes) => {
    setNodes((current) => {
      const updated = applyNodeChanges(changes, current);
      const positions: Record<string, { x: number; y: number }> = {};
      updated.forEach((node) => {
        positions[node.id] = node.position;
        if (node.selected) {
          setSelectedNodeId(node.id);
        }
      });
      setManualPositions(positions);
      return updated;
    });
  };

  const terminalBackground =
    effectiveColorScheme === "dark" ? theme.colors.dark[7] : theme.white;
  const buildLogTitle = buildEndedAt
    ? `Build Log ${buildEndedAt
        .toISOString()
        .replace("T", " ")
        .replace("Z", "")
        .slice(0, 19)}`
    : "Build Log";

  return (
    <SidebarLayout
      sidebarWidth={360}
      sidebarStyle={{
        background:
          effectiveColorScheme === "dark"
            ? "rgba(20, 20, 23, 0.92)"
            : "rgba(250, 250, 250, 0.95)",
        paddingLeft:
          "calc(var(--mantine-spacing-md) - var(--mantine-spacing-xs))",
        paddingRight:
          "calc(var(--mantine-spacing-md) - var(--mantine-spacing-xs))",
      }}
      mainStyle={{ minHeight: 0 }}
      sidebar={
        <>
          <Box
            style={{
              flexShrink: 0,
              paddingBottom: theme.spacing.xs,
              paddingLeft: theme.spacing.xs,
              paddingRight: theme.spacing.xs,
            }}
          >
            <SidebarHeader />
          </Box>

        <Box
          style={{
            flex: 1,
            overflowY: "auto",
            marginTop: theme.spacing.xs,
            paddingBottom: theme.spacing.md,
            paddingLeft: theme.spacing.xs,
            paddingRight: theme.spacing.xs,
          }}
        >
          <Stack spacing="md">
            <BuildControlsPanel
              targets={targets}
              selectedTargets={selectedTargets}
              onTargetsChange={setSelectedTargets}
              forceBuild={forceBuild}
              retryBuild={retryBuild}
              onForceBuildChange={setForceBuild}
              onRetryBuildChange={setRetryBuild}
              logBuild={logBuild}
              onLogBuildChange={setLogBuild}
              debugBuild={debugBuild}
              onDebugBuildChange={setDebugBuild}
              projects={projects}
              selectedProjects={selectedProjects}
              onProjectsChange={setSelectedProjects}
              configuration={configuration}
              onConfigurationChange={setConfiguration}
              environment={environment}
              onEnvironmentChange={setEnvironment}
              engine={engine}
              onEngineChange={setEngine}
              parallelism={parallelism}
              onParallelismChange={setParallelism}
              buildRunning={buildRunning}
              buildError={buildError}
              onStartBuild={startBuild}
              onCopyBuildCommand={copyBuildCommand}
            />

            <BuildDetailsPanel
              graph={graph}
              nodeCount={nodeCount}
              rootNodeCount={rootNodeCount}
              configurationLabel={configurationLabel}
              environmentLabel={environmentLabel}
              engineLabel={engineLabel}
            />

            <NodeDetailsPanel
              selectedProject={selectedProject}
              selectedTargetKey={selectedTargetKey}
              nodeResults={nodeResults}
              onSelectTarget={showTargetLog}
            />
          </Stack>
        </Box>
        </>
      }
    >
      <Box
        style={{
          height: "100%",
          minHeight: 0,
          display: "flex",
          flexDirection: "column",
          gap: showTerminal ? theme.spacing.md : 0,
        }}
      >
        <Box style={{ flex: 1, minHeight: 0 }}>
          <GraphPanel
            graph={graph}
            graphError={graphError}
            nodes={nodes}
            edges={edges}
            onInit={(instance) => {
              flowInstance.current = instance;
            }}
            onNodesChange={handleNodesChange}
            onEdgesChange={onEdgesChange}
            onNodeClick={(_, node) =>
              loadProjectResults(node.data.meta as ProjectNode)
            }
            onNodeDragStart={(_, node) => setDraggedNodeId(node.id)}
            onNodeDragStop={() => setDraggedNodeId(null)}
            onReflow={() => {
              setManualPositions({});
              setLayoutVersion((value) => value + 1);
            }}
          />
        </Box>

        <BuildLogPanel
          showTerminal={showTerminal}
          buildRunning={buildRunning}
          title={buildLogTitle}
          onHide={() => setShowTerminal(false)}
          terminalRef={terminalRef}
          background={terminalBackground}
        />
      </Box>
    </SidebarLayout>
  );
};

export default App;
