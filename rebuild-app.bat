@echo off
echo Rebuilding and restarting the CodeExplorer app container...
docker compose up -d --build codeexplorer
if %ERRORLEVEL% neq 0 (
    echo.
    echo Error: Failed to rebuild and restart the container.
    exit /b %ERRORLEVEL%
)
echo.
echo Container codeexplorer has been successfully rebuilt and restarted!
