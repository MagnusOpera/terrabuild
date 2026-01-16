import { Badge, Button, Paper, Stack, Text } from "@mantine/core";
import { GraphNode, ProjectNode, TargetSummary } from "../types";

type NodeDetailsPanelProps = {
  selectedProject: ProjectNode | null;
  selectedTargetKey: string | null;
  nodeResults: Record<string, TargetSummary>;
  onSelectTarget: (key: string, target: GraphNode) => void;
};

const NodeDetailsPanel = ({
  selectedProject,
  selectedTargetKey,
  nodeResults,
  onSelectTarget,
}: NodeDetailsPanelProps) => {
  return (
    <Paper withBorder p="md" radius="md" shadow="md">
      <Stack spacing="xs">
        <Text fw={600}>Node Details</Text>
        {selectedProject ? (
          <>
            <Text fw={600}>{selectedProject.directory}</Text>
            <Stack spacing="xs">
              {selectedProject.targets.map((target) => {
                const cacheKey =
                  `${target.projectHash}/${target.target}/${target.targetHash}`;
                const summary = nodeResults[cacheKey];
                return (
                  <Button
                    key={cacheKey}
                    variant={selectedTargetKey === cacheKey ? "filled" : "light"}
                    color={selectedTargetKey === cacheKey ? "blue" : "gray"}
                    onClick={() => onSelectTarget(cacheKey, target)}
                    rightSection={
                      summary ? (
                        <Badge color={summary.isSuccessful ? "green" : "red"}>
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
  );
};

export default NodeDetailsPanel;
