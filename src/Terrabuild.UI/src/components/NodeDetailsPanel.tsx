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
  const sortedTargets = selectedProject
    ? [...selectedProject.targets].sort((left, right) => {
        const leftKey = `${left.projectHash}/${left.target}/${left.targetHash}`;
        const rightKey = `${right.projectHash}/${right.target}/${right.targetHash}`;
        const leftSummary = nodeResults[leftKey];
        const rightSummary = nodeResults[rightKey];
        const leftTime = leftSummary
          ? Date.parse(leftSummary.startedAt || leftSummary.endedAt)
          : Number.POSITIVE_INFINITY;
        const rightTime = rightSummary
          ? Date.parse(rightSummary.startedAt || rightSummary.endedAt)
          : Number.POSITIVE_INFINITY;
        if (leftTime !== rightTime) {
          return leftTime - rightTime;
        }
        return left.target.localeCompare(right.target);
      })
    : [];
  return (
    <Paper withBorder p="md" radius="md" shadow="md">
      <Stack spacing="xs">
        <Text fw={600}>Node Details</Text>
        {selectedProject ? (
          <>
            <Text fw={600}>{selectedProject.directory}</Text>
            <Stack spacing="xs">
              {sortedTargets.map((target) => {
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
