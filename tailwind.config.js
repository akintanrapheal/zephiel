/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './src/SterlingLams.Web/Views/**/*.cshtml',
    './src/SterlingLams.Web/Areas/**/*.cshtml',
    './src/SterlingLams.Web/Pages/**/*.cshtml',
    './src/SterlingLams.Web/wwwroot/js/**/*.js',
  ],
  theme: {
    extend: {
      fontFamily: {
        // Editorial display / masthead. The actual family is admin-selectable (Settings → Homepage →
        // Display font) and injected as the --display-font CSS variable by _Layout. The `bodoni` name
        // is kept so existing `font-bodoni` markup keeps working regardless of the chosen font.
        bodoni: ['var(--display-font)', 'Georgia', 'serif'],
        cormorant: ['"Cormorant Garamond"', 'Georgia', 'serif'],
        inter: ['Inter', 'system-ui', 'sans-serif'],
      },
      colors: {
        // Subtle warm off-white page canvas — matches the Featured Pieces section (bg-stone-50)
        // so the whole storefront shares that soft near-white shade.
        canvas: '#fafaf9',
        gold: {
          50:  '#fdf9f0',
          100: '#faefd4',
          200: '#f4d98d',
          300: '#ecc54a',
          400: '#e2ac1f',
          500: '#c99210',
          600: '#a87209',
          700: '#85550c',
          800: '#6d4411',
          900: '#5a3812',
        },
        // Glamstar brand — soft/light purple.
        brand: {
          50:  '#faf5ff',
          100: '#f3e8ff',
          200: '#e9d5ff',
          300: '#d8b4fe',
          400: '#c084fc',
          500: '#a855f7',
          600: '#9333ea',
          700: '#7e22ce',
          800: '#6b21a8',
          900: '#581c87',
        },
      },
      letterSpacing: {
        'extra-wide': '0.3em',
        'ultra-wide': '0.5em',
      },
    },
  },
  plugins: [],
};
