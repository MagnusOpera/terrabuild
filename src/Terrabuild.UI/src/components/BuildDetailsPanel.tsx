import { Group, Paper, Stack, Text } from "@mantine/core";
import { GraphResponse } from "../types";

type BuildDetailsPanelProps = {
  graph: GraphResponse | null;
  nodeCount: number;
  rootNodeCount: number;
  configurationLabel: string;
  environmentLabel: string;
  engineLabel: string;
};

const BuildDetailsPanel = ({
  graph,
  nodeCount,
  rootNodeCount,
  configurationLabel,
  environmentLabel,
  engineLabel,
}: BuildDetailsPanelProps) => {
  return (
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
  );
};

export default BuildDetailsPanel;
