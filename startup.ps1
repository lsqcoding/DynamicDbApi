<#
.SYNOPSIS
DynamicDbApi Startup Script

.DESCRIPTION
This script is used to start nginx and DynamicDbApi application, supporting multiple .NET Runtime versions.

.PARAMETER Environment
Specifies the running environment, default is Development
Valid values: Development, Production

.PARAMETER NginxPath
Path to nginx executable, default is nginx-1.28.0 directory in project root

.PARAMETER AppPath
Application directory, default is the directory where the script is located

.EXAMPLE
.\startup.ps1 -Environment Production

.EXAMPLE
.\startup.ps1 -NginxPath "D:\nginx\nginx.exe"
#>

param (
    [ValidateSet("Development", "Production")]
    [string]$Environment = "Development",
    
    [string]$NginxPath = "$PSScriptRoot\nginx-1.28.0\nginx.exe",
    
    [string]$AppPath = $PSScriptRoot
)

# Logging function
function Write-Log {
    param (
        [string]$Message,
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# Error handling function
function Handle-Error {
    param (
        [string]$ErrorMessage
    )
    
    Write-Log "Error: $ErrorMessage" "ERROR"
    Write-Log "Script execution failed. Please check the above error message." "ERROR"
    exit 1
}

# Function to stop all processes
function Stop-AllProcesses {
    param (
        [System.Diagnostics.Process]$NginxProcess,
        [System.Diagnostics.Process]$AppProcess
    )
    
    Write-Log "Stopping all processes..." "INFO"
    
    # Stop application
    if ($AppProcess -and !$AppProcess.HasExited) {
        try {
            # Get all child processes
            $childProcesses = Get-WmiObject -Class Win32_Process | Where-Object {$_.ParentProcessId -eq $AppProcess.Id}
            foreach ($child in $childProcesses) {
                Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
            }
            
            # Stop main process
            Stop-Process -Id $AppProcess.Id -Force -ErrorAction SilentlyContinue
            Write-Log "Application stopped." "INFO"
        } catch {
            Write-Log "Failed to stop application: $($_.Exception.Message)" "ERROR"
        }
    }
    
    # Stop nginx
    if ($NginxProcess -and !$NginxProcess.HasExited) {
        try {
            # Send nginx stop command with explicit config file path
            $nginxDir = Split-Path $NginxPath
            $nginxConfig = "$nginxDir\conf\nginx.conf"
            & $NginxPath -c $nginxConfig -s stop
            Write-Log "nginx stopped." "INFO"
        } catch {
            # Force stop if command fails
            try {
                Stop-Process -Id $NginxProcess.Id -Force -ErrorAction SilentlyContinue
                Write-Log "nginx stopped forcefully." "INFO"
            } catch {
                Write-Log "Failed to stop nginx: $($_.Exception.Message)" "ERROR"
            }
        }
    }
    
    # Ensure all nginx processes are stopped
    try {
        $nginxProcesses = Get-Process -Name "nginx" -ErrorAction SilentlyContinue
        if ($nginxProcesses) {
            Stop-Process -Name "nginx" -Force -ErrorAction SilentlyContinue
            Write-Log "All nginx processes stopped." "INFO"
        }
    } catch {
        Write-Log "Failed to stop nginx processes: $($_.Exception.Message)" "ERROR"
    }
}

Write-Log "Starting project..." "INFO"

# Check if nginx path exists
if (-not (Test-Path $NginxPath)) {
    Handle-Error "nginx executable not found: $NginxPath"
}

# Check if application directory exists
if (-not (Test-Path $AppPath)) {
    Handle-Error "Application directory not found: $AppPath"
}

# Locate .NET 8 Runtime
Write-Log "Looking for .NET 8 Runtime..." "INFO"

# Use dotnet --list-runtimes to find installed .NET Runtime versions
try {
    $dotnetRuntimes = dotnet --list-runtimes 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Handle-Error "Failed to execute dotnet command. Please ensure .NET SDK or Runtime is installed."
    }
} catch {
    Handle-Error "Exception when executing dotnet command: $($_.Exception.Message)"
}

# Find .NET 8 Runtime
$net8Runtime = $dotnetRuntimes | Where-Object { $_ -match "Microsoft\.NETCore\.App 8\." }

if (-not $net8Runtime) {
    Handle-Error ".NET 8 Runtime not found. Please install .NET 8 Runtime or SDK."
}

Write-Log "Found .NET 8 Runtime: $net8Runtime" "INFO"

# Start nginx
Write-Log "Starting nginx..." "INFO"
try {
    $nginxDir = Split-Path $NginxPath
    # Specify the config file explicitly
    $nginxConfig = "$nginxDir\conf\nginx.conf"
    $nginxProcess = Start-Process -FilePath $NginxPath -ArgumentList "-c", $nginxConfig -WorkingDirectory $nginxDir -PassThru
    Write-Log "nginx started successfully." "INFO"
} catch {
    Handle-Error "Failed to start nginx: $($_.Exception.Message)"
}

# Start application
Write-Log "Starting DynamicDbApi application..." "INFO"

# Build startup command
$appDll = "$AppPath\bin\$Environment\net8.0\DynamicDbApi.dll"

# Fallback to Debug if Environment path doesn't exist
if (-not (Test-Path $appDll)) {
    Write-Log "Application DLL not found in $Environment, trying Debug..." "INFO"
    $appDll = "$AppPath\bin\Debug\net8.0\DynamicDbApi.dll"
}

if (-not (Test-Path $appDll)) {
    Handle-Error "Application DLL not found: $appDll. Please build the project first."
}

try {
    # Start application and pass process object
    $appProcess = Start-Process -FilePath "dotnet" -ArgumentList $appDll, "--environment", $Environment -WorkingDirectory $AppPath -PassThru
    Write-Log "DynamicDbApi application started successfully." "INFO"
} catch {
    Handle-Error "Failed to start application: $($_.Exception.Message)"
}

Write-Log "Project startup completed!" "INFO"
Write-Log "Access address: http://localhost:8152 (HTTP) or http://localhost:8443 (HTTPS)" "INFO"
Write-Log "Swagger documentation: http://localhost:8152/swagger/index.html" "INFO"
Write-Log "Press Enter to exit and stop all processes..." "INFO"

# Register Ctrl+C handler only
[Console]::TreatControlCAsInput = $true

# Monitor parent process and stop all processes when parent exits
Write-Log "Starting process monitoring..." "INFO"

# Get parent process ID
$parentPid = (Get-WmiObject Win32_Process -Filter "ProcessId=$PID").ParentProcessId

# Start a background job to monitor parent process
$monitorJob = Start-Job -ScriptBlock {
    param($parentId, $appProcessId, $nginxProcessId, $nginxPath, $nginxDir)
    
    while ($true) {
        try {
            # Check if parent process is still running
            $parentProcess = Get-Process -Id $parentId -ErrorAction SilentlyContinue
            if (-not $parentProcess) {
                Write-Host "`n[INFO] Parent process terminated, stopping all child processes..."
                
                # Stop application and its children
                if ($appProcessId) {
                    $childProcesses = Get-WmiObject Win32_Process | Where-Object { $_.ParentProcessId -eq $appProcessId }
                    foreach ($child in $childProcesses) {
                        Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
                    }
                    Stop-Process -Id $appProcessId -Force -ErrorAction SilentlyContinue
                }
                
                # Stop nginx gracefully with explicit config path
                if ($nginxPath -and $nginxDir) {
                    $nginxConfig = "$nginxDir\conf\nginx.conf"
                    & $nginxPath -c $nginxConfig -s stop 2>$null
                }
                
                # Force stop any remaining nginx processes
                Get-Process -Name "nginx" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
                
                break
            }
        } catch {
            # Ignore errors
        }
        
        Start-Sleep -Seconds 1
    }
} -ArgumentList $parentPid, $appProcess.Id, $nginxProcess.Id, $NginxPath, $nginxDir

# Wait for user input
Read-Host -Prompt "Press Enter to exit and stop all processes"

# Stop all processes
Stop-AllProcesses -NginxProcess $nginxProcess -AppProcess $appProcess

Write-Log "All processes stopped. Exiting..." "INFO"