import { type CSSProperties, type ReactNode } from "react";
import { Box, type MantineSpacing } from "@mantine/core";

type SidebarLayoutProps = {
  sidebar: ReactNode;
  children: ReactNode;
  sidebarWidth?: number;
  sidebarPadding?: MantineSpacing;
  mainPadding?: MantineSpacing;
  sidebarStyle?: CSSProperties;
  mainStyle?: CSSProperties;
};

const SidebarLayout = ({
  sidebar,
  children,
  sidebarWidth = 360,
  sidebarPadding = "md",
  mainPadding = "md",
  sidebarStyle,
  mainStyle,
}: SidebarLayoutProps) => {
  return (
    <Box style={{ height: "100vh", display: "flex" }}>
      <Box
        p={sidebarPadding}
        style={{
          width: sidebarWidth,
          minWidth: sidebarWidth,
          display: "flex",
          flexDirection: "column",
          height: "100vh",
          borderRight: "1px solid rgba(15, 23, 42, 0.08)",
          ...sidebarStyle,
        }}
      >
        {sidebar}
      </Box>
      <Box
        p={mainPadding}
        style={{
          flex: 1,
          minWidth: 0,
          height: "100vh",
          display: "flex",
          flexDirection: "column",
          ...mainStyle,
        }}
      >
        {children}
      </Box>
    </Box>
  );
};

export default SidebarLayout;
