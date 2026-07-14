@echo off
REM Ariel Vertex — start the API (http://localhost:5041). DB auto-creates & seeds on first run.
cd /d "%~dp0backend\src\ArielVertex.Api"
echo Starting Ariel Vertex API on http://localhost:5041 ...
dotnet run
