# csharpier-format.ps1
# Claude Code PostToolUse hook — Edit/Write 직후 .cs 파일이면 CSharpier 자동 포맷.
# .claude/settings.json hooks 섹션에서 호출.
#
# 입력: stdin 으로 JSON ({ "tool_input": { "file_path": "..." }, ... })
# 동작: file_path 가 .cs 면 dotnet csharpier format <file>

$ErrorActionPreference = 'SilentlyContinue'

try {
    $raw  = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

    $json = $raw | ConvertFrom-Json
    $file = $json.tool_input.file_path
    if (-not $file) { exit 0 }
    if (-not ($file -like '*.cs')) { exit 0 }
    if (-not (Test-Path $file)) { exit 0 }

    & dotnet csharpier format $file 2>&1 | Out-Null
} catch {
    # hook 실패가 본 작업을 막지 않도록 silent
    exit 0
}
exit 0
