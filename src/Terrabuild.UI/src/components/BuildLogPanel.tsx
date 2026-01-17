import { ActionIcon, Badge, Box, Group, Paper, Title } from "@mantine/core";
import { IconSquareRoundedChevronDown } from "@tabler/icons-react";
import { RefObject } from "react";

type BuildLogPanelProps = {
  showTerminal: boolean;
  buildRunning: boolean;
  title: string;
  onHide: () => void;
  terminalRef: RefObject<HTMLDivElement | null>;
  background: string;
};

const BuildLogPanel = ({
  showTerminal,
  buildRunning,
  title,
  onHide,
  terminalRef,
  background,
}: BuildLogPanelProps) => {
  return (
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
        <Group justify="space-between" align="center" mb="sm">
          <Title order={4}>{title}</Title>
          <Group spacing="xs" align="center" justify="flex-end">
            <Badge color={buildRunning ? "orange" : "gray"}>
              {buildRunning ? "Live" : "Idle"}
            </Badge>
            <ActionIcon
              size="lg"
              variant="subtle"
              onClick={onHide}
              disabled={buildRunning}
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
        style={{ background }}
      />
    </Paper>
  );
};

export default BuildLogPanel;
