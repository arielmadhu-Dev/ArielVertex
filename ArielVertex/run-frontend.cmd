@echo off
REM Ariel Vertex — start the web app (http://localhost:5173). Installs deps on first run.
cd /d "%~dp0frontend"
if not exist node_modules (
  echo Installing frontend dependencies ^(first run^)...
  call npm install
)
echo Starting Ariel Vertex web app on http://localhost:5173 ...
call npm run dev
