import { ActionIcon, useMantineColorScheme } from "@mantine/core";
import { IconMoon, IconSun, IconSunMoon } from "@tabler/icons-react";

const ThemeSwitcher = () => {
  const { colorScheme, setColorScheme } = useMantineColorScheme();

  const ThemeIcon = () => {
    if (colorScheme === "auto") {
      return <IconSunMoon size={18} />;
    }
    if (colorScheme === "light") {
      return <IconSun size={18} />;
    }
    return <IconMoon size={18} />;
  };

  const toggleTheme = () => {
    if (colorScheme === "auto") {
      setColorScheme("light");
    } else if (colorScheme === "light") {
      setColorScheme("dark");
    } else {
      setColorScheme("auto");
    }
  };

  return (
    <ActionIcon
      onClick={toggleTheme}
      variant="subtle"
      size="lg"
      aria-label="Toggle color scheme"
    >
      <ThemeIcon />
    </ActionIcon>
  );
};

export default ThemeSwitcher;
