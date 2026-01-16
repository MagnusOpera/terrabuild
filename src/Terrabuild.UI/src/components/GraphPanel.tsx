import { ActionIcon, Box, Group, Paper, Text, Title } from "@mantine/core";
import { IconAffiliate } from "@tabler/icons-react";
import ReactFlow, {
  Background,
  Controls,
  Edge,
  Node,
  NodeMouseHandler,
  OnEdgesChange,
  OnNodesChange,
  ReactFlowInstance,
} from "reactflow";
import { GraphResponse } from "../types";

type GraphPanelProps = {
  graph: GraphResponse | null;
  graphError: string | null;
  nodes: Node[];
  edges: Edge[];
  onInit: (instance: ReactFlowInstance) => void;
  onNodesChange: OnNodesChange;
  onEdgesChange: OnEdgesChange;
  onNodeClick: NodeMouseHandler;
  onNodeDragStart: NodeMouseHandler;
  onNodeDragStop: NodeMouseHandler;
  onReflow: () => void;
};

const GraphPanel = ({
  graph,
  graphError,
  nodes,
  edges,
  onInit,
  onNodesChange,
  onEdgesChange,
  onNodeClick,
  onNodeDragStart,
  onNodeDragStop,
  onReflow,
}: GraphPanelProps) => {
  return (
    <Paper
      withBorder
      shadow="md"
      radius="md"
      p="md"
      style={{
        flex: 1,
        display: "flex",
        flexDirection: "column",
        minHeight: 0,
        height: "100%",
      }}
    >
      <Group justify="space-between" align="center" mb="sm">
        <Group spacing="xs" align="center">
          <Title order={4}>Execution Graph</Title>
          {graphError && (
            <Text size="sm" c="red">
              {graphError}
            </Text>
          )}
        </Group>
        <Group spacing="xs" align="center" justify="flex-end">
          <ActionIcon
            size="lg"
            variant="subtle"
            onClick={onReflow}
            aria-label="Reflow graph"
          >
            <IconAffiliate size={18} />
          </ActionIcon>
        </Group>
      </Group>
      <Box
        className="reactflow-wrapper"
        style={{ flex: 1, minHeight: 0, width: "100%", height: "100%" }}
      >
        {graph ? (
          <Box style={{ width: "100%", height: "100%" }}>
            <ReactFlow
              nodes={nodes}
              edges={edges}
              fitView
              fitViewOptions={{ padding: 0.5, minZoom: 0.1 }}
              minZoom={0.1}
              onInit={onInit}
              proOptions={{ hideAttribution: true }}
              nodesDraggable
              elementsSelectable
              panOnDrag
              onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onNodeClick={onNodeClick}
            onNodeDragStart={onNodeDragStart}
            onNodeDragStop={onNodeDragStop}
          >
              <Background gap={24} />
              <Controls position="bottom-right" />
            </ReactFlow>
          </Box>
        ) : (
          <Group position="center" style={{ height: "100%" }}>
            <Text size="sm" c="dimmed">
              Select at least one target to view the graph.
            </Text>
          </Group>
        )}
      </Box>
    </Paper>
  );
};

export default GraphPanel;
