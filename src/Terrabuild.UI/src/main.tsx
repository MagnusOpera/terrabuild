import React from "react";
import ReactDOM from "react-dom/client";
import { ColorScheme, ColorSchemeProvider, MantineProvider } from "@mantine/core";
import { Notifications } from "@mantine/notifications";
import { useLocalStorage } from "@mantine/hooks";
import App from "./App";
import "./styles.css";

const Root = () => {
  const [colorScheme, setColorScheme] = useLocalStorage<ColorScheme>({
    key: "tb-color-scheme",
    defaultValue: "dark",
    getInitialValueInEffect: true,
  });

  const toggleColorScheme = (value?: ColorScheme) =>
    setColorScheme(value || (colorScheme === "dark" ? "light" : "dark"));

  const notificationTheme = {
    colorScheme,
    components: {
      Notification: {
        styles: (theme: any) => ({
          root: {
            border: `1px solid ${theme.colors.gray[3]}`,
            borderRadius: 8,
          },
        }),
      },
    },
  };

  return (
    <ColorSchemeProvider
      colorScheme={colorScheme}
      toggleColorScheme={toggleColorScheme}
    >
      <MantineProvider
        withGlobalStyles
        withNormalizeCSS
        theme={notificationTheme}
      >
        <Notifications position="top-right" />
        <App />
      </MantineProvider>
    </ColorSchemeProvider>
  );
};

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Root />
  </React.StrictMode>
);
