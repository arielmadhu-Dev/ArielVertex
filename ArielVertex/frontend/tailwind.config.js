/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Ariel Vertex brand
        navy: {
          DEFAULT: '#17203E',
          50: '#f3f5fa', 100: '#e5e9f3', 200: '#c3cce0', 300: '#9aa8c9',
          400: '#5f719b', 500: '#33406b', 600: '#222e52', 700: '#17203E',
          800: '#111935', 900: '#0c1228',
        },
        brand: {
          DEFAULT: '#1E7FD4',
          50: '#eef6fe', 100: '#d8ebfc', 200: '#b0d6f8', 300: '#7ab8f1',
          400: '#4098e6', 500: '#1E7FD4', 600: '#1665b1', 700: '#134f8d',
          800: '#143f6e', 900: '#12365c',
        },
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'Segoe UI', 'sans-serif'],
      },
      boxShadow: {
        card: '0 1px 2px rgba(16,24,40,.04), 0 4px 16px rgba(16,24,40,.06)',
        pop: '0 12px 40px rgba(16,24,40,.16)',
      },
      borderRadius: { xl: '0.9rem', '2xl': '1.15rem' },
      keyframes: {
        shimmer: { '100%': { transform: 'translateX(100%)' } },
      },
      animation: { shimmer: 'shimmer 1.6s infinite' },
    },
  },
  plugins: [],
}
