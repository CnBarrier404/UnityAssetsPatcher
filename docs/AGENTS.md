# AGENTS.md

This is a document site for [Unity Assets Patcher](https://github.com/CnBarrier404/UnityAssetsPatcher).

## Commands

| Command           | Action                                       |
| :---------------- | :------------------------------------------- |
| `npm install`     | Installs dependencies                        |
| `npm run dev`     | Starts local dev server at `localhost:4321`  |
| `npm run build`   | Builds the production site to `./dist/`      |
| `npm run preview` | Previews the production build locally        |
| `npx astro ...`   | Runs commands using the local Astro install   |

## Repository Structure

| Path                | Purpose                                                                          |
| :------------------ | :------------------------------------------------------------------------------- |
| `.astro/`           | Astro-generated development metadata; should not be edited manually              |
| `dist/`             | Generated production build output for deployment; should not be edited manually  |
| `public/`           | Static assets copied directly to the final build without processing              |
| `src/`              | Main source code for pages, components, layouts, styles, content, and site logic |
| `astro.config.mjs`  | Astro configuration, integrations, Markdown processing, and build settings       |
| `package.json`      | Project metadata, scripts, and dependencies                                       |
| `package-lock.json` | Locked npm dependency graph; update it together with dependency changes          |
| `tsconfig.json`     | TypeScript and Astro compiler configuration                                      |

## Documentation

Full documentation: https://starlight.astro.build/getting-started/
