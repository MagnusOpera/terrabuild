import {
  Accordion,
  ActionIcon,
  Button,
  Checkbox,
  Group,
  MultiSelect,
  NumberInput,
  Paper,
  Select,
  Stack,
  Text,
  TextInput,
  useMantineColorScheme,
  useMantineTheme,
} from "@mantine/core";
import { useMediaQuery } from "@mantine/hooks";
import { IconCopy } from "@tabler/icons-react";
import { ProjectInfo } from "../types";

const engineOptions = [
  { value: "default", label: "Default" },
  { value: "none", label: "None" },
  { value: "docker", label: "Docker" },
  { value: "podman", label: "Podman" },
];

type BuildControlsPanelProps = {
  targets: string[];
  selectedTargets: string[];
  onTargetsChange: (values: string[]) => void;
  forceBuild: boolean;
  retryBuild: boolean;
  onForceBuildChange: (checked: boolean) => void;
  onRetryBuildChange: (checked: boolean) => void;
  projects: ProjectInfo[];
  selectedProjects: string[];
  onProjectsChange: (values: string[]) => void;
  configuration: string;
  onConfigurationChange: (value: string) => void;
  environment: string;
  onEnvironmentChange: (value: string) => void;
  engine: string;
  onEngineChange: (value: string) => void;
  parallelism: string;
  onParallelismChange: (value: string) => void;
  buildRunning: boolean;
  buildError: string | null;
  onStartBuild: () => void;
  onCopyBuildCommand: () => void;
};

const BuildControlsPanel = ({
  targets,
  selectedTargets,
  onTargetsChange,
  forceBuild,
  retryBuild,
  onForceBuildChange,
  onRetryBuildChange,
  projects,
  selectedProjects,
  onProjectsChange,
  configuration,
  onConfigurationChange,
  environment,
  onEnvironmentChange,
  engine,
  onEngineChange,
  parallelism,
  onParallelismChange,
  buildRunning,
  buildError,
  onStartBuild,
  onCopyBuildCommand,
}: BuildControlsPanelProps) => {
  const theme = useMantineTheme();
  const { colorScheme } = useMantineColorScheme();
  const prefersDark = useMediaQuery("(prefers-color-scheme: dark)");
  const effectiveColorScheme =
    colorScheme === "auto" ? (prefersDark ? "dark" : "light") : colorScheme;

  return (
    <Paper withBorder p="md" radius="md" shadow="md">
      <Stack spacing="sm">
        <MultiSelect
          data={targets.map((target) => ({ value: target, label: target }))}
          label="Targets"
          placeholder="Select targets"
          searchable
          nothingFoundMessage="No targets"
          value={selectedTargets}
          onChange={onTargetsChange}
        />

        <Group spacing="md">
          <Checkbox
            label="Force"
            checked={forceBuild}
            onChange={(event) => {
              const checked = event.currentTarget.checked;
              onForceBuildChange(checked);
              if (checked) {
                onRetryBuildChange(false);
              }
            }}
          />
          <Checkbox
            label="Retry"
            checked={retryBuild}
            onChange={(event) => {
              const checked = event.currentTarget.checked;
              onRetryBuildChange(checked);
              if (checked) {
                onForceBuildChange(false);
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
                effectiveColorScheme === "dark"
                  ? theme.colors.dark[7]
                  : theme.white,
            },
            content: { paddingLeft: 0, paddingRight: 0 },
            panel: {
              paddingLeft: 0,
              paddingRight: 0,
              backgroundColor:
                effectiveColorScheme === "dark"
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
                  nothingFoundMessage="No projects"
                  value={selectedProjects}
                  onChange={onProjectsChange}
                />

                <TextInput
                  label="Configuration"
                  placeholder="default"
                  value={configuration}
                  onChange={(event) =>
                    onConfigurationChange(event.currentTarget.value)
                  }
                />

                <TextInput
                  label="Environment"
                  placeholder="default"
                  value={environment}
                  onChange={(event) =>
                    onEnvironmentChange(event.currentTarget.value)
                  }
                />

                <Select
                  data={engineOptions}
                  label="Engine"
                  value={engine}
                  onChange={(value) => onEngineChange(value ?? "default")}
                />

                <NumberInput
                  label="Parallelism"
                  placeholder="auto"
                  min={1}
                  value={parallelism === "" ? undefined : Number(parallelism)}
                  onChange={(value) => {
                    if (value === "" || value === null) {
                      onParallelismChange("");
                    } else {
                      onParallelismChange(String(value));
                    }
                  }}
                />
              </Stack>
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion>

        <Group spacing="xs" wrap="nowrap">
          <Button
            onClick={onStartBuild}
            disabled={buildRunning || selectedTargets.length === 0}
            style={{ flex: 1 }}
          >
            {buildRunning ? "Building..." : "Build"}
          </Button>
          <ActionIcon
            size="lg"
            variant="light"
            onClick={onCopyBuildCommand}
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
  );
};

export default BuildControlsPanel;
