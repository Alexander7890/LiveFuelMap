/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,jsx}"],
  darkMode: ["selector", ':is([data-theme="dark"], [data-theme="gray"])'],
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#e7fbfb",
          100: "#c9f3f3",
          200: "#96e4e5",
          300: "#5ecfd1",
          400: "#28b4b7",
          500: "#0f8b8d",
          600: "#0c7376",
          700: "#0d5b5e",
          800: "#0f484b",
          900: "#102f33"
        },
        fuel: {
          amber: "#f2b544",
          green: "#72b01d",
          red: "#dc3545",
          blue: "#2f80ed"
        }
      },
      fontFamily: {
        sans: ["Inter", "Segoe UI", "Arial", "sans-serif"]
      },
      borderRadius: {
        card: "0.75rem",
        panel: "1rem"
      },
      boxShadow: {
        soft: "0 18px 55px rgba(15, 47, 51, 0.12)",
        panel: "0 12px 32px rgba(15, 47, 51, 0.10)",
        glow: "0 0 0 1px rgba(15, 139, 141, 0.18), 0 16px 36px rgba(15, 139, 141, 0.16)"
      },
      keyframes: {
        shimmer: {
          "0%": { backgroundPosition: "200% 0" },
          "100%": { backgroundPosition: "-200% 0" }
        },
        floatIn: {
          "0%": { opacity: "0", transform: "translateY(12px)" },
          "100%": { opacity: "1", transform: "translateY(0)" }
        },
        sunSweep: {
          "0%": { opacity: "0", backgroundPosition: "120% 0" },
          "45%": { opacity: ".8", backgroundPosition: "40% 0" },
          "100%": { opacity: "0", backgroundPosition: "-35% 0" }
        }
      },
      animation: {
        shimmer: "shimmer 1.8s linear infinite",
        floatIn: "floatIn .45s ease both",
        sunSweep: "sunSweep 1.15s ease-out both"
      },
      screens: {
        xs: "420px"
      }
    }
  },
  plugins: []
};
