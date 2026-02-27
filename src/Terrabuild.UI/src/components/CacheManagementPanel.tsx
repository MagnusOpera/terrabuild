import { Button, Checkbox, Group, Paper, Stack, Text } from "@mantine/core";

type CacheManagementPanelProps = {
  clearCache: boolean;
  clearHome: boolean;
  onClearCacheChange: (checked: boolean) => void;
  onClearHomeChange: (checked: boolean) => void;
  onClearCache: () => void;
  clearCacheDisabled: boolean;
  clearCacheRunning: boolean;
  disabled: boolean;
};

const CacheManagementPanel = ({
  clearCache,
  clearHome,
  onClearCacheChange,
  onClearHomeChange,
  onClearCache,
  clearCacheDisabled,
  clearCacheRunning,
  disabled,
}: CacheManagementPanelProps) => {
  return (
    <Paper withBorder p="md" radius="md" shadow="md">
      <fieldset
        disabled={disabled}
        style={{ margin: 0, padding: 0, border: "none", minInlineSize: 0 }}
      >
        <Stack spacing="sm">
          <Text fw={600}>Cache Management</Text>
          <Group spacing="md">
            <Checkbox
              label="Cache"
              checked={clearCache}
              onChange={(event) =>
                onClearCacheChange(event.currentTarget.checked)
              }
            />
            <Checkbox
              label="Home"
              checked={clearHome}
              onChange={(event) =>
                onClearHomeChange(event.currentTarget.checked)
              }
            />
          </Group>
          <Button
            color="red"
            onClick={onClearCache}
            disabled={clearCacheDisabled}
            loading={clearCacheRunning}
          >
            Clear Cache
          </Button>
        </Stack>
      </fieldset>
    </Paper>
  );
};

export default CacheManagementPanel;
