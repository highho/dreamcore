# Probe: does a real mouse click on the title's Start button start the game?
# Verify3D gate 9 can only prove the Button *references* are wired -- the onClick listener is
# added at runtime by TitleScreen.Start() (an editor script's AddListener would not serialize),
# so "the button is dead in the build" is invisible to every editor-side gate. This clicks it.
# Coordinates are desktop pixels in the same space CopyFromScreen captures (both virtualized
# under DPI scaling), read off logs/shots12/00_title.png. ASCII only: PS 5.1 reads .ps1 as ANSI.
param(
  [Parameter(Mandatory = $true)][string]$Exe,
  [Parameter(Mandatory = $true)][string]$OutDir,
  # Target as a fraction of the window's *client* rect. The Title canvas is a 1920x1080
  # CanvasScaler and the start button sits at anchoredPosition (-190, -230) from the centre,
  # so x = 0.5 - 190/1920 and y = 0.5 + 230/1080 (UI y grows downward, canvas y grows upward).
  # Deriving it from the client rect instead of pasting desktop pixels read off a screenshot is
  # what keeps this honest across runs: the window lands wherever Windows puts it, and the two
  # literal pixel pairs an earlier run used pointed at empty wall as soon as it moved.
  [double]$FX = 0.4010417,
  [double]$FY = 0.7129630,
  [int]$BootWaitSec = 15
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
public static class Win32Click {
  [StructLayout(LayoutKind.Sequential)]
  public struct RECT { public int left; public int top; public int right; public int bottom; }
  [StructLayout(LayoutKind.Sequential)]
  public struct POINT { public int x; public int y; }
  [StructLayout(LayoutKind.Sequential)]
  public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Explicit)]
  public struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MOUSEINPUT mi; }
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool fAltTab);
  // Foreground stealing is refused to a process that holds no input focus at all: SetForegroundWindow
  // alone silently failed through 12 retries while a second Qoder window kept the desk. Tapping ALT
  // makes this process the "last input sender", which is the condition Windows checks, and
  // SwitchToThisWindow is the olderAlt-Tab-equivalent path that still works when the newer API is denied.
  public static void TakeFocus(IntPtr h) {
    keybd_event(0x12, 0, 0, UIntPtr.Zero);              // VK_MENU down
    keybd_event(0x12, 0, 0x0002, UIntPtr.Zero);         // and up
    SetForegroundWindow(h);
    SwitchToThisWindow(h, true);
  }
  // Does the whole origin+size read in C#: PowerShell's [ref] marshalling of a struct `out`
  // parameter handed back an Object[] instead of the RECT, so the arithmetic never ran.
  public static int[] ClientBox(IntPtr h) {
    RECT r; if (!GetClientRect(h, out r)) throw new Exception("GetClientRect failed");
    POINT p0 = new POINT(); if (!ClientToScreen(h, ref p0)) throw new Exception("ClientToScreen failed");
    return new int[] { p0.x, p0.y, r.right - r.left, r.bottom - r.top };
  }
  public static void ClickAt(int x, int y) {
    // SendInput, not the deprecated mouse_event: an earlier run of this probe used mouse_event
    // and *both* title buttons read as dead, while the keyboard path in shot3d.ps1 worked.
    // Injected mouse_event traffic is not guaranteed to reach a Raw Input consumer, which is
    // what Unity's input stack is; SendInput is the documented way to post hardware-equivalent
    // events. The cursor is parked over the target first, then the pair is sent twice -- a
    // window that lost focus can swallow its first click (activation), and a dead button must
    // not be diagnosed from one swallowed press.
    SetCursorPos(x, y);
    Thread.Sleep(400);
    for (int i = 0; i < 2; i++) {
      INPUT[] down = new INPUT[1];
      down[0].type = 0; down[0].mi.dwFlags = 0x0002;           // MOUSEEVENTF_LEFTDOWN
      INPUT[] up = new INPUT[1];
      up[0].type = 0; up[0].mi.dwFlags = 0x0004;               // MOUSEEVENTF_LEFTUP
      SendInput((uint)down.Length, down, Marshal.SizeOf(typeof(INPUT)));
      Thread.Sleep(150);
      SendInput((uint)up.Length, up, Marshal.SizeOf(typeof(INPUT)));
      Thread.Sleep(500);
      SetCursorPos(x, y);
      Thread.Sleep(200);
    }
  }
}
"@
if (-not (Test-Path $Exe)) { throw "no exe: $Exe" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Exe = (Resolve-Path $Exe).Path
$OutDir = (Resolve-Path $OutDir).Path
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

function Get-MeanLuma($bmp) {
  $sum = [double]0; $n = 0
  for ($y = 0; $y -lt $bmp.Height; $y += 7) {
    for ($x = 0; $x -lt $bmp.Width; $x += 7) {
      $c = $bmp.GetPixel($x, $y)
      $sum += 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
      $n++
    }
  }
  return $sum / [double]$n
}

function Take-Shot([string]$name) {
  $path = Join-Path $OutDir $name
  $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $l = Get-MeanLuma $bmp
  $g.Dispose(); $bmp.Dispose()
  Write-Host ("SHOT {0} bytes={1} luma={2:F1}" -f $name, (Get-Item $path).Length, $l)
  return $l
}

function Assert-Foreground([string]$when) {
  # A synthesized click goes to whatever holds foreground. An earlier run of this probe had
  # another app take the window between the screenshot and the click, and the "button is dead"
  # reading it produced was really "nobody clicked the game". Refuse instead of guessing.
  $p.Refresh()
  if ($p.MainWindowHandle -eq [IntPtr]::Zero) { throw "player has no main window handle ($when)" }
  $fg = [Win32Click]::GetForegroundWindow()
  if ($fg -ne $p.MainWindowHandle) {
    throw ("foreground is not the player ($when): fg=$fg want=$($p.MainWindowHandle) -- a click here would land in another app")
  }
}

function Resolve-Click([double]$fx, [double]$fy) {
  $o = [Win32Click]::ClientBox($p.MainWindowHandle)
  $x = [int]($o[0] + $fx * $o[2]); $y = [int]($o[1] + $fy * $o[3])
  Write-Host ("client origin=$($o[0]),$($o[1]) size=$($o[2])x$($o[3]) -> click $x,$y (fx=$fx fy=$fy)")
  return @($x, $y)
}

function Grab-Foreground() {
  # A single SetForegroundWindow loses to whatever else is alive on this desktop (a second
  # Qoder session held foreground through one whole run). Windows only lets a background app
  # steal focus in limited cases, so retry instead of assuming one call is enough.
  for ($i = 0; $i -lt 12; $i++) {
    $p.Refresh()
    [Win32Click]::TakeFocus($p.MainWindowHandle)
    Start-Sleep -Milliseconds 400
    $p.Refresh()
    if ([Win32Click]::GetForegroundWindow() -eq $p.MainWindowHandle) { return $true }
  }
  return $false
}

$p = Start-Process $Exe -ArgumentList @(
  "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720",
  "-logFile", (Join-Path $OutDir "player.log")
) -PassThru
try {
  Start-Sleep -Seconds $BootWaitSec
  if ($p.HasExited) { throw ("player exited early, code=" + $p.ExitCode) }
  [void](Grab-Foreground)
  # Pin the player above everything else for the whole probe. A mouse click is delivered to
  # the window *under the cursor*, not to the foreground window, so the pin is what keeps the
  # screenshot honest: two earlier runs had another app (stable handle 262696) cover the game
  # between click and shot, and the "click did nothing" reading was really "we photographed
  # somebody else's window". The click itself still needs the player focused, because Windows
  # spends a first click on activating an unfocused window -- hence Assert-Foreground below.
  [void][Win32Click]::SetWindowPos($p.MainWindowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x13)  # TOPMOST | NOSIZE | NOMOVE | NOACTIVATE
  Start-Sleep -Milliseconds 800
  Assert-Foreground "before the title shot"
  $before = Take-Shot "a_before_click.png"
  # The before-frame has to be the dark title page or the whole comparison is worthless.
  # This desktop is shared: another session's Enter/Space lands in whatever window holds
  # foreground, and TitleScreen treats those keys as "start", so the corridor can already be
  # on screen a second before we click (that is what made run 11 report "CLICK DID NOTHING"
  # on both frames at luma ~76). Refuse to draw a verdict from a menu we were never on.
  if ($before -gt 45) {
    throw ("before-frame is not the title page (luma=$before) -- something already left the menu before this probe clicked; verdict would be meaningless")
  }

  $xy = Resolve-Click $FX $FY
  [void][Win32Click]::SetCursorPos($xy[0], $xy[1])
  Start-Sleep -Milliseconds 600
  if (-not (Grab-Foreground)) { Assert-Foreground "after moving the cursor" }
  Assert-Foreground "before the click"
  [Win32Click]::ClickAt($xy[0], $xy[1])
  Write-Host "clicked at $($xy[0]),$($xy[1])"
  Start-Sleep -Seconds 3
  [void][Win32Click]::SetWindowPos($p.MainWindowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x13)
  $p.Refresh()
  Write-Host "player alive after click: $(-not $p.HasExited)"
  $after = Take-Shot "b_after_click.png"

  Write-Host ("luma before={0:F1} after={1:F1}" -f $before, $after)
  if ($after -gt 45 -and $after -gt $before * 2) { Write-Host "CLICK STARTS THE GAME: dark menu -> lit corridor" }
  else { Write-Host "CLICK DID NOTHING: still on the dark title page (button wiring broken?)" }
}
finally {
  if (-not $p.HasExited) { [void]$p.Kill(); Start-Sleep -Seconds 2 }
  Write-Host "done"
}
