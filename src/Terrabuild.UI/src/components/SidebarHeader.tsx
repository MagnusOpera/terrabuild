import { Box, Group, Text, Title } from "@mantine/core";
import ThemeSwitcher from "./ThemeSwitcher";

const SidebarHeader = () => {
  return (
    <Group justify="space-between" align="center">
      <Group spacing="sm" align="center">
        <Box
          component="svg"
          xmlns="http://www.w3.org/2000/svg"
          width={40}
          height={36}
          viewBox="-450 -20 800 600"
          aria-hidden="true"
        >
          <defs>
            <linearGradient
              id="Gradient_1"
              gradientUnits="userSpaceOnUse"
              x1="818.3"
              y1="493.056"
              x2="1320.3"
              y2="493.056"
            >
              <stop offset="0" stopColor="#FF0080" />
              <stop offset="1" stopColor="#FF8000" />
            </linearGradient>
            <linearGradient
              id="Gradient_2"
              gradientUnits="userSpaceOnUse"
              x1="561.3"
              y1="486.056"
              x2="1063.3"
              y2="486.056"
            >
              <stop offset="0" stopColor="#7A7ECF" />
              <stop offset="1" stopColor="#00BBFF" />
            </linearGradient>
          </defs>
          <g id="Layer_1" transform="translate(-1000, -206.057)">
            <path
              d="M1285.1,426.55 L959.85,226.6 C899.51,189.51 822.14,232.94 821.53,304.25 L821.28,333.21 L861.81,333.21 C870.58,286.46 926.09,260.4 970.14,286.42 L1227.66,438.53 C1264.82,460.97 1264.82,513.44 1227.66,535.88 L970.46,691.22 C922.46,720.21 860.72,687.98 858.16,634.21 L818.69,634.21 L818.31,678.35 C817.67,752.27 898.1,797.82 960.27,758.74 L1285.11,554.53 C1332.03,525.02 1332.03,456.06 1285.1,426.55 z"
              fill="url(#Gradient_1)"
            />
            <path
              d="M1028.1,419.55 L702.85,219.6 C642.51,182.51 565.14,225.94 564.53,297.25 L561.31,671.35 C560.67,745.27 641.1,790.82 703.27,751.74 L1028.11,547.53 C1075.03,518.02 1075.03,449.06 1028.1,419.55 z M973.1,532.82 L720.09,688.16 C671.67,717.89 609.03,683.24 609.52,627.01 L612.03,342.43 C612.51,288.19 672.77,255.14 719.77,283.36 L973.1,435.47 C1009.66,457.91 1009.66,510.37 973.1,532.82 z"
              fill="url(#Gradient_2)"
            />
          </g>
        </Box>
        <Box>
          <Text size="xs" tt="uppercase" fw={600} c="dimmed">
            Terrabuild
          </Text>
          <Title order={3}>Graph Console</Title>
        </Box>
      </Group>
      <ThemeSwitcher />
    </Group>
  );
};

export default SidebarHeader;
