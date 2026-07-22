param(
    [Parameter(Mandatory = $true)][ValidateSet("move", "click", "rightclick", "doubleclick", "drag", "key", "keydown", "keyup", "type", "scroll", "focus")] [string]$Action,
    [int]$X = 0,
    [int]$Y = 0,
    [int]$X2 = 0,
    [int]$Y2 = 0,
    [string]$Key = "",
    [string]$Text = "",
    [int]$Amount = 0,
    [int]$HoldMs = 60,
    [string]$WindowTitle = "",
    [int]$WindowPid = 0
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class DpiFix { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
[DpiFix]::SetProcessDPIAware() | Out-Null

$sig = @'
using System;
using System.Runtime.InteropServices;
public static class Win32Input {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
    public const uint LEFTDOWN = 0x02, LEFTUP = 0x04, RIGHTDOWN = 0x08, RIGHTUP = 0x10, WHEEL = 0x0800;
}
'@
if (-not ([System.Management.Automation.PSTypeName]'Win32Input').Type) {
    Add-Type -TypeDefinition $sig
}

function Get-VK([string]$name) {
    switch ($name.ToUpper()) {
        "ENTER" { 0x0D }; "ESC" { 0x1B }; "ESCAPE" { 0x1B }; "SPACE" { 0x20 }
        "TAB" { 0x09 }; "SHIFT" { 0x10 }; "CTRL" { 0x11 }; "ALT" { 0x12 }
        "LEFT" { 0x25 }; "UP" { 0x26 }; "RIGHT" { 0x27 }; "DOWN" { 0x28 }
        "F1" { 0x70 }; "F2" { 0x71 }; "F3" { 0x72 }; "F4" { 0x73 }; "F5" { 0x74 }
        "F6" { 0x75 }; "F7" { 0x76 }; "F8" { 0x77 }; "F9" { 0x78 }; "F10" { 0x79 }
        "F11" { 0x7A }; "F12" { 0x7B }; "DELETE" { 0x2E }; "BACKSPACE" { 0x08 }
        default {
            if ($name.Length -eq 1) { [int][char]$name.ToUpper() }
            else { throw "Unknown key: $name" }
        }
    }
}

function Press-VK([int]$vk, [int]$holdMs) {
    $scan = [Win32Input]::MapVirtualKey([uint32]$vk, 0)
    [Win32Input]::keybd_event([byte]$vk, [byte]$scan, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds $holdMs
    [Win32Input]::keybd_event([byte]$vk, [byte]$scan, 2, [IntPtr]::Zero)
}

switch ($Action) {
    "focus" {
        if ($WindowPid -ne 0) {
            $h = (Get-Process -Id $WindowPid).MainWindowHandle
        }
        else {
            $h = [Win32Input]::FindWindow($null, $WindowTitle)
        }
        if ($h -eq [IntPtr]::Zero) { throw "Window not found: $WindowTitle / pid $WindowPid" }
        [Win32Input]::SetForegroundWindow($h) | Out-Null
        Start-Sleep -Milliseconds 300
        Write-Output "Focused handle $h"
    }
    "move" {
        [Win32Input]::SetCursorPos($X, $Y) | Out-Null
        Write-Output "Moved to $X,$Y"
    }
    "click" {
        [Win32Input]::SetCursorPos($X, $Y) | Out-Null
        Start-Sleep -Milliseconds 80
        [Win32Input]::mouse_event([Win32Input]::LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds $HoldMs
        [Win32Input]::mouse_event([Win32Input]::LEFTUP, 0, 0, 0, [IntPtr]::Zero)
        Write-Output "Clicked $X,$Y"
    }
    "rightclick" {
        [Win32Input]::SetCursorPos($X, $Y) | Out-Null
        Start-Sleep -Milliseconds 80
        [Win32Input]::mouse_event([Win32Input]::RIGHTDOWN, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds $HoldMs
        [Win32Input]::mouse_event([Win32Input]::RIGHTUP, 0, 0, 0, [IntPtr]::Zero)
        Write-Output "Right-clicked $X,$Y"
    }
    "doubleclick" {
        [Win32Input]::SetCursorPos($X, $Y) | Out-Null
        Start-Sleep -Milliseconds 80
        for ($i = 0; $i -lt 2; $i++) {
            [Win32Input]::mouse_event([Win32Input]::LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 40
            [Win32Input]::mouse_event([Win32Input]::LEFTUP, 0, 0, 0, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 60
        }
        Write-Output "Double-clicked $X,$Y"
    }
    "drag" {
        [Win32Input]::SetCursorPos($X, $Y) | Out-Null
        Start-Sleep -Milliseconds 100
        [Win32Input]::mouse_event([Win32Input]::LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 100
        $steps = 20
        for ($i = 1; $i -le $steps; $i++) {
            $nx = $X + [int](($X2 - $X) * $i / $steps)
            $ny = $Y + [int](($Y2 - $Y) * $i / $steps)
            [Win32Input]::SetCursorPos($nx, $ny) | Out-Null
            Start-Sleep -Milliseconds 15
        }
        [Win32Input]::mouse_event([Win32Input]::LEFTUP, 0, 0, 0, [IntPtr]::Zero)
        Write-Output "Dragged $X,$Y -> $X2,$Y2"
    }
    "key" {
        Press-VK (Get-VK $Key) $HoldMs
        Write-Output "Pressed $Key"
    }
    "keydown" {
        $vk = Get-VK $Key
        $scan = [Win32Input]::MapVirtualKey([uint32]$vk, 0)
        [Win32Input]::keybd_event([byte]$vk, [byte]$scan, 0, [IntPtr]::Zero)
        Write-Output "KeyDown $Key"
    }
    "keyup" {
        $vk = Get-VK $Key
        $scan = [Win32Input]::MapVirtualKey([uint32]$vk, 0)
        [Win32Input]::keybd_event([byte]$vk, [byte]$scan, 2, [IntPtr]::Zero)
        Write-Output "KeyUp $Key"
    }
    "type" {
        foreach ($ch in $Text.ToCharArray()) {
            [System.Windows.Forms.SendKeys]::SendWait([regex]::Escape([string]$ch))
            Start-Sleep -Milliseconds 30
        }
        Write-Output "Typed '$Text'"
    }
    "scroll" {
        [Win32Input]::SetCursorPos($X, $Y) | Out-Null
        Start-Sleep -Milliseconds 80
        [Win32Input]::mouse_event([Win32Input]::WHEEL, 0, 0, $Amount, [IntPtr]::Zero)
        Write-Output "Scrolled $Amount at $X,$Y"
    }
}
