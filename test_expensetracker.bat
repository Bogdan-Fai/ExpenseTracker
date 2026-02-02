@echo off
echo === Testing ExpenseTracker Application ===
echo.

echo 1. Building the project...
cd /d "5лабаООП\ExpenseTracker"
dotnet build
if errorlevel 1 (
    echo Build failed!
    exit /b 1
)
echo Build successful!
echo.

echo 2. Running the application (Task 1 - Import from TXT)...
dotnet run -- 1
echo.

echo 3. Generating monthly report (Task 1)...
dotnet run -- 2
echo.

echo 4. Filtering and exporting transactions (Task 2)...
dotnet run -- 3
echo.

echo 5. Stream import from JSONL (Task 3)...
dotnet run -- 4
echo.

echo 6. Sync from multiple sources (Task 4)...
dotnet run -- 5
echo.

echo 7. Running tests...
cd Tests
dotnet test
echo.

echo === All tests completed! ===
pause
