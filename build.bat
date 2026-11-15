@echo off
REM Gera o Sleeve.exe (arquivo unico e portatil) na pasta "dist"
cd /d "%~dp0"

REM 1. Fecha o Sleeve se estiver aberto (inclusive escondido na bandeja)
taskkill /im Sleeve.exe /f >nul 2>&1
timeout /t 1 /nobreak >nul

REM 2. Limpa as sobras do build anterior (so arquivos gerados pelo build)
if exist src\windows\bin\Release rd /s /q src\windows\bin\Release
if exist src\windows\obj\Release rd /s /q src\windows\obj\Release
if exist dist\Sleeve.exe del /f /q dist\Sleeve.exe

REM 3. Gera o .exe
dotnet publish src\windows\Sleeve.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o dist

echo.
if errorlevel 1 (
  echo O build falhou. Veja as mensagens acima.
) else (
  echo Pronto: dist\Sleeve.exe
)
pause
