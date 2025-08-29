import React, { useEffect, useState } from "react";
import { ThemeContext, type Theme } from "../contexts/ThemeContext";

interface ThemeProviderProps {
  children: React.ReactNode;
}

const ThemeProvider: React.FC<ThemeProviderProps> = ({ children }) => {
  const [theme, setTheme] = useState<Theme>(() => {
    // Get theme from localStorage or default to light
    const savedTheme = localStorage.getItem("meeting-processor-theme") as Theme;
    return savedTheme || "light";
  });

  useEffect(() => {
    // Apply theme to document root
    document.documentElement.setAttribute("data-theme", theme);
    // Save theme to localStorage
    localStorage.setItem("meeting-processor-theme", theme);
  }, [theme]);

  const toggleTheme = () => {
    setTheme((prevTheme) => (prevTheme === "light" ? "dark" : "light"));
  };

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
};

export default ThemeProvider;
