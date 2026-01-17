import { ActionIcon, Badge, Box, Group, Paper, Title } from "@mantine/core";
import {
  IconSquareRoundedChevronDown,
  IconSquareRoundedChevronUp,
} from "@tabler/icons-react";
import { Ref } from "react";

type BuildLogPanelProps = {
  showTerminal: boolean;
  buildRunning: boolean;
  title: string;
  onToggle: () => void;
  terminalRef: Ref<HTMLDivElement>;
  background: string;
};

const BuildLogPanel = ({
  showTerminal,
  buildRunning,
  title,
  onToggle,
  terminalRef,
  background,
}: BuildLogPanelProps) => {
  const collapsedHeight = 72;
  return (
    <Paper
      withBorder
      shadow={showTerminal ? "md" : "sm"}
      radius="md"
      p="md"
      style={{
        height: showTerminal ? 280 : collapsedHeight,
        display: "flex",
        flexDirection: "column",
        overflow: "hidden",
        transition: "height 200ms ease",
      }}
    >
      <Group justify="space-between" align="center" mb={showTerminal ? "sm" : 0}>
        <Title order={4}>{title}</Title>
        <Group align="center" justify="flex-end">
          <Badge color={buildRunning ? "orange" : "gray"}>
            {buildRunning ? "Live" : "Idle"}
          </Badge>
          <ActionIcon
            size="lg"
            variant="subtle"
            onClick={onToggle}
            aria-label={showTerminal ? "Collapse terminal" : "Expand terminal"}
          >
            {showTerminal ? (
              <IconSquareRoundedChevronDown size={18} />
            ) : (
              <IconSquareRoundedChevronUp size={18} />
            )}
          </ActionIcon>
        </Group>
      </Group>
      <Box
        className="terminal-body"
        ref={terminalRef}
        style={{
          background,
          flexGrow: showTerminal ? 1 : 0,
          flexBasis: showTerminal ? "auto" : 0,
          minHeight: 0,
          maxHeight: showTerminal ? "none" : 0,
          opacity: showTerminal ? 1 : 0,
          pointerEvents: showTerminal ? "auto" : "none",
          transition: "opacity 200ms ease",
        }}
      />
    </Paper>
  );
};

export default BuildLogPanel;
