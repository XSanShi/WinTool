#Requires -Version 5.1
<#
.SYNOPSIS
    WinTool 发布版验收脚本：用 UI 自动化真的点一遍三类功能，然后校验落盘文件。

.DESCRIPTION
    覆盖：
      1. 待办：加两条 → 勾选第一条 → 清理已完成 → 断言 todos.json 只剩未完成项、留底 .md 出现 `- [x]`；
      2. 账号密码：新建 → 填标题/用户名 → 打开"显示密码" → 填密码 → 保存 → 断言 vault.json 有该标题；
      3. 随笔：新建 → 写正文 → 保存 → 断言 notes.json 出现该正文；
      4. 隐藏到悬浮球 → 断言主窗口消失。

    脚本自己判定 PASS/FAIL，失败退出码 1。会被脚本写进去的测试数据只落在被测目录里，
    所以请对**空目录的发布版**运行，不要对着你日常用的数据跑。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File build\smoke.ps1 -Exe dist\WinTool-win-x64\WinTool.exe
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$ProcessName = 'WinTool',
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

$failures = New-Object System.Collections.Generic.List[string]
function Check([bool]$condition, [string]$what) {
    if ($condition) {
        Write-Host "  ok   $what" -ForegroundColor Green
    }
    else {
        Write-Host "  FAIL $what" -ForegroundColor Red
        $failures.Add($what)
    }
}

function Find-ById([object]$scope, [string]$id) {
    if ($null -eq $scope) { return $null }
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    return $scope.FindFirst($TS::Descendants, $cond)
}

function Find-AllByType([object]$scope, [object]$type) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $type)
    return $scope.FindAll($TS::Descendants, $cond)
}

function Invoke-It([object]$element, [string]$label) {
    if ($null -eq $element) { Check $false "找不到控件: $label"; return }
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
    }
    else {
        Check $false "控件不支持 Invoke: $label"
    }
}

function Set-Text([object]$element, [string]$text, [string]$label) {
    if ($null -eq $element) { Check $false "找不到控件: $label"; return }
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        $pattern.SetValue($text)
    }
    else {
        Check $false "控件不支持 ValuePattern: $label"
    }
}

function Select-Nav([object]$win, [int]$index) {
    $nav = Find-ById $win 'NavList'
    $items = $nav.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::ListItem)))
    $pattern = $null
    if ($items[$index].TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
    }
    Start-Sleep -Milliseconds 700
}

# ---------- 启动 ----------
$Exe = (Resolve-Path $Exe).Path
$exeDir = Split-Path -Parent $Exe
$dataDir = Join-Path $exeDir 'data'
$archiveDir = Join-Path $exeDir '待办留底'

Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

Write-Host "启动 $Exe" -ForegroundColor Cyan
$proc = Start-Process -FilePath $Exe -PassThru

$deadline = (Get-Date).AddSeconds(30)
$handle = [IntPtr]::Zero
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 400
    $proc.Refresh()
    if ($proc.HasExited) { throw '进程启动后立即退出' }
    if ($proc.MainWindowHandle -ne 0) { $handle = $proc.MainWindowHandle; break }
}
if ($handle -eq [IntPtr]::Zero) { throw '30 秒内没有出现主窗口' }

$win = $AE::FromHandle($handle)
Write-Host "主窗口: $($win.Current.Name)" -ForegroundColor Cyan
Check ($win.Current.Name -like '*WinTool*') '窗口标题正确'

# ---------- 1. 待办 ----------
Write-Host '待办' -ForegroundColor Cyan
Select-Nav $win 0
$todoInput = Find-ById $win 'TodoInput'
Set-Text $todoInput 'smoke todo A' 'TodoInput'
Invoke-It (Find-ById $win 'TodoAdd') 'TodoAdd'
Start-Sleep -Milliseconds 500
Set-Text $todoInput 'smoke todo B' 'TodoInput'
Invoke-It (Find-ById $win 'TodoAdd') 'TodoAdd'
Start-Sleep -Milliseconds 700

$boxes = Find-AllByType (Find-ById $win 'TodoList') $CT::CheckBox
Check ($boxes.Count -eq 2) "列表里有 2 条待办（实际 $($boxes.Count)）"
if ($boxes.Count -gt 0) {
    $toggle = $null
    if ($boxes[0].TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$toggle)) { $toggle.Toggle() }
    Start-Sleep -Milliseconds 600
}

Invoke-It (Find-ById $win 'TodoCleanup') 'TodoCleanup'
Start-Sleep -Milliseconds 1500

$todosPath = Join-Path $dataDir 'todos.json'
Check (Test-Path $todosPath) 'todos.json 已生成'
if (Test-Path $todosPath) {
    $todos = Get-Content $todosPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Check ($todos.items.Count -eq 1) "清理后只剩 1 条（实际 $($todos.items.Count)）"
    Check (-not $todos.items[0].done) '剩下的是未完成那条'
    Check ($todos.items[0].text -eq 'smoke todo B') '剩下的是 smoke todo B'
    Check ($todos.items[0].order -eq 0) 'order 已重排为 0'
}

$mdFiles = @()
if (Test-Path $archiveDir) { $mdFiles = Get-ChildItem $archiveDir -Filter '*.md' -File }
Check ($mdFiles.Count -ge 1) '留底 .md 已生成'
if ($mdFiles.Count -ge 1) {
    $md = Get-Content $mdFiles[0].FullName -Raw -Encoding UTF8
    Check ($md -match '- \[x\] smoke todo A') '留底里出现 - [x] smoke todo A'
    Check ($md -match 'id:\s*[0-9a-f]{32}') '留底里带条目 id'
    Check ($md -match '## \d{4}-\d{2}-\d{2} \d{2}:\d{2}') '留底里有批次段头'
}

# ---------- 2. 账号密码 ----------
Write-Host '账号密码' -ForegroundColor Cyan
Select-Nav $win 1
Invoke-It (Find-ById $win 'CredNew') 'CredNew'
Start-Sleep -Milliseconds 400
Set-Text (Find-ById $win 'CredTitle') 'smoke-credential' 'CredTitle'
Set-Text (Find-ById $win 'CredUsername') 'smoke@example.com' 'CredUsername'

$show = Find-ById $win 'CredShowPassword'
$tg = $null
if ($null -ne $show -and $show.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$tg)) { $tg.Toggle() }
Start-Sleep -Milliseconds 400
Set-Text (Find-ById $win 'CredPasswordPlain') 'smoke-p@ss' 'CredPasswordPlain'
Invoke-It (Find-ById $win 'CredSave') 'CredSave'
Start-Sleep -Milliseconds 1000

$vaultPath = Join-Path $dataDir 'vault.json'
Check (Test-Path $vaultPath) 'vault.json 已生成'
if (Test-Path $vaultPath) {
    $vaultText = Get-Content $vaultPath -Raw -Encoding UTF8
    Check ($vaultText -match 'smoke-credential') 'vault.json 里有该条目标题'
    Check ($vaultText -match '"password":\s*"smoke-p@ss"') 'vault.json 是明文（D1a 已确认的预期行为）'

    $acl = & icacls $vaultPath 2>&1 | Out-String
    Check ($acl -notmatch 'BUILTIN\\Users|Everyone') 'ACL 未包含 Users/Everyone'
}

# ---------- 3. 随笔 ----------
Write-Host '随笔' -ForegroundColor Cyan
Select-Nav $win 2
Invoke-It (Find-ById $win 'NoteNew') 'NoteNew'
Start-Sleep -Milliseconds 400
Set-Text (Find-ById $win 'NoteContent') "# smoke note`r`n`r`nbody line" 'NoteContent'
Invoke-It (Find-ById $win 'NoteSave') 'NoteSave'
Start-Sleep -Milliseconds 900

$notesPath = Join-Path $dataDir 'notes.json'
Check (Test-Path $notesPath) 'notes.json 已生成'
if (Test-Path $notesPath) {
    $notesText = Get-Content $notesPath -Raw -Encoding UTF8
    Check ($notesText -match 'smoke note') 'notes.json 里有该内容'
    Check ($notesText -notmatch '\\r\\n') '换行已归一为 \n'
}

# ---------- 4. 隐藏到悬浮球 ----------
Write-Host '悬浮球' -ForegroundColor Cyan
Invoke-It (Find-ById $win 'HideToBubble') 'HideToBubble'
Start-Sleep -Milliseconds 1200
$proc.Refresh()
Check ($proc.MainWindowHandle -eq 0) '主窗口已隐藏'

$bubble = $AE::RootElement.FindAll($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)))
$small = @($bubble | Where-Object { $_.Current.BoundingRectangle.Width -gt 0 -and $_.Current.BoundingRectangle.Width -lt 120 })
Check ($small.Count -ge 1) '存在小尺寸悬浮球窗口'

# ---------- 收尾 ----------
if (-not $KeepRunning) {
    Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
}

Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host '全部通过' -ForegroundColor Green
    exit 0
}

Write-Host "失败 $($failures.Count) 项：" -ForegroundColor Red
$failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
