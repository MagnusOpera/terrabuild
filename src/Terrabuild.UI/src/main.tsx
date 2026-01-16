import React from "react";
import ReactDOM from "react-dom/client";
import {
  MantineProvider,
  createTheme,
  localStorageColorSchemeManager,
} from "@mantine/core";
import { Notifications } from "@mantine/notifications";
import "@mantine/core/styles.css";
import "@mantine/notifications/styles.css";
import App from "./App";
import "./styles.css";

const colorSchemeManager = localStorageColorSchemeManager({
  key: "tb-color-scheme",
});

const theme = createTheme({
  components: {
    Notification: {
      styles: (mantineTheme) => ({
        root: {
          border: `1px solid ${mantineTheme.colors.gray[3]}`,
          borderRadius: 8,
        },
      }),
    },
  },
});

const Root = () => {
  return (
    <MantineProvider
      theme={theme}
      defaultColorScheme="auto"
      colorSchemeManager={colorSchemeManager}
    >
      <Notifications position="top-right" />
      <App />
    </MantineProvider>
  );
};

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Root />
  </React.StrictMode>
);
