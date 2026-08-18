import { Badge, Button, Code, Divider, Group, Paper, Stack, Text } from "@mantine/core";
import {
  GraphNode,
  NodeExplanation,
  ProjectNode,
  TargetSummary,
} from "../types";

type NodeDetailsPanelProps = {
  selectedProject: ProjectNode | null;
  selectedTargetKey: string | null;
  nodeResults: Record<string, TargetSummary>;
  explanations: Record<string, NodeExplanation>;
  onSelectTarget: (key: string, target: GraphNode) => void;
};

const NodeDetailsPanel = ({
  selectedProject,
  selectedTargetKey,
  nodeResults,
  explanations,
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
  const selectedTarget = sortedTargets.find((target) => {
    const key = `${target.projectHash}/${target.target}/${target.targetHash}`;
    return key === selectedTargetKey;
  });
  const explanation = selectedTarget
    ? explanations[selectedTarget.id]
    : undefined;

  return (
    <Paper withBorder p="md" radius="md" shadow="md">
      <Stack gap="xs">
        <Text fw={600}>Node Details</Text>
        {selectedProject ? (
          <>
            <Text fw={600}>{selectedProject.directory}</Text>
            <Stack gap="xs">
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
            {selectedTargetKey ? (
              <>
                <Divider my="xs" />
                <Text fw={600}>Why Terrabuild chose this</Text>
                {explanation ? (
                  <Stack gap="xs">
                    <Group gap="xs">
                      <Badge color={explanation.action === "exec" ? "blue" : "green"}>
                        {explanation.action ?? "Unresolved"}
                      </Badge>
                      {explanation.actionReason ? (
                        <Text size="sm">{explanation.actionReason}</Text>
                      ) : null}
                    </Group>
                    <Text size="sm">
                      Required: {explanation.required === undefined || explanation.required === null
                        ? "unresolved"
                        : explanation.required
                          ? "yes"
                          : "no"}
                      {explanation.requirementReason
                        ? ` (${explanation.requirementReason})`
                        : ""}
                    </Text>
                    <Text size="sm">
                      Cache: {explanation.cache
                        ? `${explanation.cache.lookup} in ${explanation.cache.scope}${explanation.cache.origin ? `, origin ${explanation.cache.origin}` : ""}`
                        : "not consulted"}
                    </Text>
                    {explanation.fingerprint ? (
                      <Stack gap={2}>
                        <Text size="xs" c="dimmed">Cache key</Text>
                        <Code block>{explanation.fingerprint.cacheKey}</Code>
                      </Stack>
                    ) : null}
                    {explanation.environmentSensitiveInputs.length > 0 ? (
                      <Stack gap={2}>
                        <Text size="xs" c="orange">Environment-sensitive inputs</Text>
                        <Code block color="orange">
                          {explanation.environmentSensitiveInputs
                            .map((input) => input.name)
                            .join("\n")}
                        </Code>
                      </Stack>
                    ) : null}
                    {explanation.actionDependencies.length > 0 ? (
                      <Stack gap={2}>
                        <Text size="xs" c="dimmed">Decision dependencies</Text>
                        <Code block>{explanation.actionDependencies.join("\n")}</Code>
                      </Stack>
                    ) : null}
                    {explanation.evaluationInputs.length > 0 ? (
                      <Stack gap={2}>
                        <Text size="xs" c="dimmed">Evaluated inputs</Text>
                        <Code block>
                          {explanation.evaluationInputs
                            .map((input) => `${input.name}: ${input.valueHash}`)
                            .join("\n")}
                        </Code>
                      </Stack>
                    ) : null}
                    {explanation.resolvedOperations.length > 0 ? (
                      <Stack gap="xs">
                        <Text size="xs" c="dimmed">Resolved operations</Text>
                        {explanation.resolvedOperations.map((operation, index) => (
                          <Stack key={`${operation.metaCommand}-${index}`} gap={2}>
                            <Text size="sm" fw={600}>{operation.metaCommand}</Text>
                            <Code block>
                              {[
                                `command: ${operation.command}`,
                                `arguments hash: ${operation.argumentsHash}`,
                                operation.container
                                  ? `container: ${operation.container}`
                                  : null,
                                operation.platform
                                  ? `platform: ${operation.platform}`
                                  : null,
                                `forwarded variables: ${operation.forwardedVariableNames.join(", ") || "none"}`,
                                `injected environment: ${operation.injectedEnvironment.map((item) => item.name).join(", ") || "none"}`,
                              ]
                                .filter((line): line is string => line !== null)
                                .join("\n")}
                            </Code>
                          </Stack>
                        ))}
                      </Stack>
                    ) : null}
                  </Stack>
                ) : (
                  <Text size="sm" c="dimmed">
                    No explanation is available for this graph node.
                  </Text>
                )}
              </>
            ) : null}
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
